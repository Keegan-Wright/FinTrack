using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Components.Account;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FinanceTracker.Extensions;

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
            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddScoped<IdentityUserAccessor>();
            builder.Services.AddScoped<IdentityRedirectManager>();

            AuthenticationBuilder authBuilder = builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            });

            authBuilder.AddIdentityCookies();

            if (builder.Configuration.IsOidcEnabled())
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
                .AddDefaultTokenProviders();

            builder.Services.AddSingleton<IEmailSender<FinanceTrackerUser>, IdentityNoOpEmailSender>();
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
        /// Sets the request's <see cref="CurrentUser"/>. Authentication has already run, so the user is known. Covers
        /// the static Identity pages, prerendering the client's pages, and the API the browser calls.
        /// </summary>
        public WebApplication UseCurrentUser()
        {
            app.Use((context, next) =>
            {
                context.RequestServices.GetRequiredService<CurrentUser>().Set(context.User);
                return next(context);
            });

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

            app.MapGet("/auth/oidc", (HttpContext context, string? returnUrl) =>
                TypedResults.Challenge(new AuthenticationProperties
                {
                    RedirectUri = $"/auth/oidc/complete?returnUrl={Uri.EscapeDataString(LocalPath(context.Request, returnUrl))}"
                }, [OpenIdConnectDefaults.AuthenticationScheme]));

            // The handler has signed the provider's identity into the external cookie, swap it for the local user's
            app.MapGet("/auth/oidc/complete", async Task<RedirectHttpResult> (HttpContext context, string? returnUrl,
                OidcUserLinker linker, UserManager<FinanceTrackerUser> userManager,
                SignInManager<FinanceTrackerUser> signInManager) =>
            {
                AuthenticateResult external = await context.AuthenticateAsync(IdentityConstants.ExternalScheme);
                await context.SignOutAsync(IdentityConstants.ExternalScheme);
                if (!external.Succeeded)
                {
                    return TypedResults.Redirect(OidcFailedPath);
                }

                FinanceTrackerUser? user = await linker.FindOrCreateAsync(external.Principal);
                if (user is null)
                {
                    return TypedResults.Redirect(OidcFailedPath);
                }

                if (await userManager.IsLockedOutAsync(user))
                {
                    return TypedResults.Redirect("/Account/Lockout");
                }

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
    }

    private static void ConfigureOidc(OpenIdConnectOptions options, IConfiguration configuration)
    {
        options.Authority = configuration[FinTrackEnvironment.OidcAuthority];
        options.ClientId = configuration[FinTrackEnvironment.OidcClientId];
        options.ClientSecret = configuration[FinTrackEnvironment.OidcClientSecret];

        // Authorization code flow with PKCE, signing into Identity's external cookie for /auth/oidc/complete
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.SignInScheme = IdentityConstants.ExternalScheme;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");

        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims = false;
        options.ClaimActions.MapUniqueJsonKey(OidcUserLinker.EmailVerified, OidcUserLinker.EmailVerified);
        options.ClaimActions.MapUniqueJsonKey(OidcUserLinker.PreferredUsername, OidcUserLinker.PreferredUsername);

        options.Events = new OpenIdConnectEvents
        {
            // Declining at the provider, or a misconfigured client, comes back as a failure rather than an error page
            OnRemoteFailure = context =>
            {
                context.HttpContext.RequestServices.GetRequiredService<ILogger<OidcUserLinker>>()
                    .LogOidcRemoteFailure(context.Failure);
                context.Response.Redirect(OidcFailedPath);
                context.HandleResponse();
                return Task.CompletedTask;
            }
        };
    }

    /// <summary>
    /// <paramref name="returnUrl"/> when it's on this site, as a path, otherwise the home page. The login page is
    /// given absolute urls, see <c>RedirectToLogin</c>.
    /// </summary>
    private static string LocalPath(HttpRequest request, string? returnUrl)
    {
        Uri site = new($"{request.Scheme}://{request.Host}{request.PathBase}/");
        return Uri.TryCreate(site, returnUrl, out Uri? target) && target.Authority == site.Authority
            ? target.PathAndQuery
            : "/";
    }
}
