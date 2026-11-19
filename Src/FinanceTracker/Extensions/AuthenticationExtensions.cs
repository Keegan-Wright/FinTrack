using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Components.Account;
using FinanceTracker.Components.Account.Pages;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Middleware;
using FinanceTracker.Telemetry;
using FinanceTracker.Tokens;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FinanceTracker.Extensions;

/// <summary>
/// Sets up signing in, with Identity's cookies and, when it's configured, single sign-on.
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Where a failed OIDC sign in lands.
    /// </summary>
    private const string OidcFailedPath = "/Account/Login?sso=failed";

    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// ASP.NET Identity with cookie sign in, optional OIDC sign in, and the <see cref="ICurrentUser"/> services
        /// act for.
        /// </summary>
        public WebApplicationBuilder AddFinTrackAuthentication()
        {
            // Read first, so the app won't start without a way to sign in
            SignInMethods signInMethods = builder.Configuration.GetSignInMethods();
            builder.Services.AddSingleton(signInMethods);

            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddScoped<IdentityUserAccessor>();
            builder.Services.AddScoped<IdentityRedirectManager>();

            AuthenticationBuilder authBuilder = builder.Services.AddAuthentication(options =>
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme);

            // API requests with a personal access token sign in with it, everything else with the cookie
            authBuilder.AddIdentityCookies();
            authBuilder.AddPersonalAccessTokens();

            // How long a cookie is trusted before it's checked against the account, so a locked user or one whose
            // password changed is signed out within a minute rather than Identity's default half hour
            builder.Services.Configure<SecurityStampValidatorOptions>(options =>
                options.ValidationInterval = TimeSpan.FromMinutes(1));

            if (signInMethods.SingleSignOn)
            {
                authBuilder.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme,
                    options => ConfigureOidc(options, builder.Configuration));
            }

            builder.Services.AddIdentityCore<FinanceTrackerUser>(options =>
                {
                    options.SignIn.RequireConfirmedAccount = false;
                    options.SignIn.RequireConfirmedEmail = false;
                    options.SignIn.RequireConfirmedPhoneNumber = false;
                    options.User.RequireUniqueEmail = true;
                })
                .AddRoles<FinanceTrackerRole>()
                .AddEntityFrameworkStores<FinanceTrackerContext>()
                .AddSignInManager()
                .AddFinTrackSecurity(builder.Configuration.GetPublicAddress());

            builder.Services.AddFinTrackAccountServices();
            builder.Services.AddScoped<AdminProvisioner>();
            builder.Services.AddScoped<OidcUserLinker>();

            // Services act for ICurrentUser, set per request by UseCurrentUser and per job by its scope
            builder.Services.AddCurrentUser();

            return builder;
        }
    }

    extension(WebApplication app)
    {
        /// <summary>
        /// Makes sure an existing install has its admin, see <see cref="AdminProvisioner.EnsureAdminAsync"/>. Run
        /// after migrating, the role tables have to exist.
        /// </summary>
        public async Task EnsureAdminAsync()
        {
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AdminProvisioner>().EnsureAdminAsync();
        }

        /// <summary>
        /// Sets the request's <see cref="CurrentUser"/> and tags its span and logs with them, see
        /// <see cref="CurrentUserMiddleware"/>. Covers the static Identity pages, prerendering the client's pages, and
        /// the API the browser calls.
        /// </summary>
        public WebApplication UseCurrentUser()
        {
            app.UseMiddleware<CurrentUserMiddleware>();
            return app;
        }

        /// <summary>
        /// The endpoints of an OIDC sign in, when OIDC is configured. The login page's single sign on link starts it
        /// at <c>/auth/oidc</c>, the provider returns to the handler's <c>/signin-oidc</c>, and
        /// <c>/auth/oidc/complete</c> signs the local user in.
        /// </summary>
        public WebApplication MapOidcChallenge()
        {
            if (!app.Configuration.IsOidcEnabled())
            {
                return app;
            }

            app.MapGet("/auth/oidc", ChallengeOidcAsync);

            // The handler has signed the provider's identity into the external cookie, swap it for the local user's
            app.MapGet("/auth/oidc/complete", async Task<RedirectHttpResult> (HttpContext context, string? returnUrl,
                OidcUserLinker linker, UserManager<FinanceTrackerUser> userManager,
                SignInManager<FinanceTrackerUser> signInManager, FinTrackMetrics metrics) =>
            {
                AuthenticateResult external = await context.AuthenticateAsync(IdentityConstants.ExternalScheme);
                await context.SignOutAsync(IdentityConstants.ExternalScheme);
                if (!external.Succeeded)
                {
                    metrics.SingleSignOnFailed(SingleSignOnFailure.NoIdentity);
                    return TypedResults.Redirect(OidcFailedPath);
                }

                FinanceTrackerUser? user = await linker.FindOrCreateAsync(external.Principal);
                if (user is null)
                {
                    metrics.SingleSignOnFailed(SingleSignOnFailure.NoAccount);
                    return TypedResults.Redirect(OidcFailedPath);
                }

                if (await userManager.IsLockedOutAsync(user))
                {
                    metrics.SingleSignOnFailed(SingleSignOnFailure.LockedOut);
                    return TypedResults.Redirect($"/{Lockout.PathFor(user)}");
                }

                // The provider's admins group decides, before the cookie's roles are read
                await linker.MatchAdminRoleAsync(user, external.Principal);

                // Builds the cookie the same way as a password sign in, roles and the first name included
                await signInManager.SignInAsync(user, isPersistent: false);
                return TypedResults.Redirect(LocalPath(context.Request, returnUrl));
            });

            return app;
        }
    }

    extension(IConfiguration configuration)
    {
        /// <summary>
        /// OIDC sign in is optional, it's on when the authority, client id and secret are all configured.
        /// </summary>
        public bool IsOidcEnabled() =>
            !string.IsNullOrEmpty(configuration[FinTrackEnvironment.OidcAuthority]) &&
            !string.IsNullOrEmpty(configuration[FinTrackEnvironment.OidcClientId]) &&
            !string.IsNullOrEmpty(configuration[FinTrackEnvironment.OidcClientSecret]);

        /// <summary>
        /// The ways people can sign in. Password sign in is on unless <see cref="FinTrackEnvironment.PasswordSignIn"/>
        /// turns it off, single sign-on when <see cref="IsOidcEnabled"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Both are off, so nobody could sign in.</exception>
        public SignInMethods GetSignInMethods()
        {
            SignInMethods methods = new()
            {
                Password = configuration.GetValue(FinTrackEnvironment.PasswordSignIn, true),
                SingleSignOn = configuration.IsOidcEnabled()
            };

            if (!methods.Password && !methods.SingleSignOn)
            {
                throw new InvalidOperationException(
                    $"{FinTrackEnvironment.PasswordSignIn} is false and single sign-on isn't set up, so nobody could " +
                    $"sign in. Set {FinTrackEnvironment.OidcAuthority}, {FinTrackEnvironment.OidcClientId} and " +
                    $"{FinTrackEnvironment.OidcClientSecret}, or turn password sign in back on.");
            }

            return methods;
        }
    }

    /// <summary>
    /// Sets up single sign-on with the provider in the <c>OIDC_*</c> settings.
    /// </summary>
    /// <param name="options">The handler's options.</param>
    /// <param name="configuration">The app's settings.</param>
    private static void ConfigureOidc(OpenIdConnectOptions options, IConfiguration configuration)
    {
        options.Authority = configuration[FinTrackEnvironment.OidcAuthority];
        options.ClientId = configuration[FinTrackEnvironment.OidcClientId];
        options.ClientSecret = configuration[FinTrackEnvironment.OidcClientSecret];
        options.RequireHttpsMetadata = !configuration.GetValue(FinTrackEnvironment.OidcAllowHttp, false);

        // Authorization code flow with PKCE, signing into Identity's external cookie for /auth/oidc/complete
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.SignInScheme = IdentityConstants.ExternalScheme;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");

        // The provider's groups, the admins group deciding who's an admin, see OidcUserLinker.MatchAdminRoleAsync
        options.Scope.Add(OidcUserLinker.Groups);

        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims = false;
        options.ClaimActions.MapUniqueJsonKey(OidcUserLinker.EmailVerified, OidcUserLinker.EmailVerified);
        options.ClaimActions.MapUniqueJsonKey(OidcUserLinker.PreferredUsername, OidcUserLinker.PreferredUsername);

        // User info's groups array becomes one claim per group
        options.ClaimActions.MapJsonKey(OidcUserLinker.Groups, OidcUserLinker.Groups);

        options.Events = new OpenIdConnectEvents
        {
            // Declining at the provider, or a misconfigured client, comes back as a failure rather than an error page
            OnRemoteFailure = context =>
            {
                IServiceProvider services = context.HttpContext.RequestServices;
                services.GetRequiredService<ILogger<OidcUserLinker>>().LogOidcRemoteFailure(context.Failure);
                services.GetRequiredService<FinTrackMetrics>().SingleSignOnFailed(SingleSignOnFailure.Provider);
                context.Response.Redirect(OidcFailedPath);
                context.HandleResponse();
                return Task.CompletedTask;
            }
        };
    }

    /// <summary>
    /// Sends the user to the OIDC provider to sign in, coming back to <paramref name="returnUrl"/>. When the provider
    /// refuses before that, e.g. this site's address isn't one it knows, can't be reached, or its settings can't be
    /// read, the user lands on the sign in page saying single sign on failed, as when it fails at the provider, rather
    /// than on the error page.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="returnUrl">Where to go once signed in.</param>
    public static async Task<IResult> ChallengeOidcAsync(HttpContext context, string? returnUrl)
    {
        AuthenticationProperties properties = new()
        {
            RedirectUri = $"/auth/oidc/complete?returnUrl={Uri.EscapeDataString(LocalPath(context.Request, returnUrl))}"
        };

        try
        {
            await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, properties);
            return TypedResults.Empty;
        }
        catch (Exception exception) when (IsProviderFailure(exception))
        {
            context.RequestServices.GetRequiredService<ILogger<OidcUserLinker>>().LogOidcChallengeFailed(exception);
            context.RequestServices.GetRequiredService<FinTrackMetrics>()
                .SingleSignOnFailed(SingleSignOnFailure.NotStarted);
            return TypedResults.Redirect(OidcFailedPath);
        }
    }

    /// <summary>
    /// Whether the provider failed the challenge. It refused (<see cref="OpenIdConnectProtocolException"/>), its
    /// settings couldn't be read (<see cref="InvalidOperationException"/> from the configuration manager) or it
    /// couldn't be reached (<see cref="HttpRequestException"/>).
    /// </summary>
    /// <param name="exception">What the challenge threw.</param>
    private static bool IsProviderFailure(Exception exception) =>
        exception is OpenIdConnectProtocolException or InvalidOperationException or HttpRequestException;

    /// <summary>
    /// <paramref name="returnUrl"/> when it's on this site, as a path, otherwise the home page. The login page is
    /// given absolute urls, see <c>RedirectToLogin</c>.
    /// </summary>
    /// <param name="request">The request, whose site the url has to be on.</param>
    /// <param name="returnUrl">Where to go once signed in.</param>
    private static string LocalPath(HttpRequest request, string? returnUrl)
    {
        Uri site = new($"{request.Scheme}://{request.Host}{request.PathBase}/");
        return Uri.TryCreate(site, returnUrl, out Uri? target) && target.Authority == site.Authority
            ? target.PathAndQuery
            : "/";
    }

    extension(IServiceCollection services)
    {
        /// <summary>
        /// What the account pages, <see cref="IProfileService"/> and <see cref="IUsersService"/> use: Identity's
        /// emails, the links they carry, confirming an email, resetting a password, and the emails saying sign in
        /// changed.
        /// </summary>
        public IServiceCollection AddFinTrackAccountServices()
        {
            services.AddSingleton<IEmailSender<FinanceTrackerUser>, IdentityEmailSender>();
            services.AddHttpContextAccessor();
            services.AddScoped<IAccountUrls, AccountUrls>();
            services.AddScoped<IEmailConfirmation, EmailConfirmation>();
            services.AddScoped<IEmailChange, EmailChange>();
            services.AddScoped<IProfileService, ProfileService>();
            services.AddScoped<IPasskeyService, PasskeyService>();
            services.AddScoped<IPasswordReset, PasswordReset>();
            services.AddScoped<IUsersService, UsersService>();
            services.AddScoped<ISecurityEmails, SecurityEmails>();
            return services;
        }
    }
}
