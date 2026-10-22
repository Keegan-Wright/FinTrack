using System.Security.Claims;
using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Components.Account;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FinanceTracker.Extensions;

public static class AuthenticationExtensions
{
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
            builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

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

            // Services act for ICurrentUser, set per request by UseCurrentUser, per circuit by the handler, per job
            // by its scope
            builder.Services.AddCurrentUser();
            builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<CircuitHandler, CurrentUserCircuitHandler>());

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
        /// static and prerendered pages and endpoints, interactive circuits are handled by
        /// <see cref="CurrentUserCircuitHandler"/>.
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
        /// The endpoint the login page's single sign on button starts an OIDC sign in from, when OIDC is configured.
        /// </summary>
        public WebApplication MapOidcChallenge()
        {
            if (!app.Configuration.IsOidcEnabled())
            {
                return app;
            }

            app.MapGet("/auth/oidc", async (HttpContext context) =>
            {
                await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties
                {
                    RedirectUri = "/"
                });
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

        options.ResponseType = OpenIdConnectResponseType.Code;
        options.SaveTokens = true;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");

        options.ClaimActions.MapJsonKey(ClaimTypes.GivenName, "given_name");
        options.ClaimActions.MapJsonKey(ClaimTypes.Surname, "family_name");

        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            NameClaimType = "name"
        };

        options.Events = new OpenIdConnectEvents
        {
            OnTokenValidated = LinkOidcUserAsync
        };
    }

    /// <summary>
    /// Finds or creates the local user for an OIDC sign in and adds their id to the principal, so
    /// <see cref="UserManager{TUser}"/> recognises the signed in user.
    /// </summary>
    private static async Task LinkOidcUserAsync(TokenValidatedContext context)
    {
        ClaimsPrincipal? principal = context.Principal;
        string? oidcSub = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        string? email = principal?.FindFirst(ClaimTypes.Email)?.Value;

        if (principal is null || string.IsNullOrEmpty(oidcSub)) return;

        // Users are matched and created by email, without one a sign in could link to the wrong account
        if (string.IsNullOrEmpty(email))
        {
            context.Fail("The identity provider did not supply an email address.");
            return;
        }

        IDbContextFactory<FinanceTrackerContext> factory = context.HttpContext.RequestServices
            .GetRequiredService<IDbContextFactory<FinanceTrackerContext>>();

        await using FinanceTrackerContext dbContext = await factory.CreateDbContextAsync();

        FinanceTrackerUser? user = await dbContext.Users.FirstOrDefaultAsync(u => u.OpenIdConnectSubject == oidcSub)
                                   ?? await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            string firstName = principal.FindFirst(ClaimTypes.GivenName)?.Value ?? string.Empty;
            string lastName = principal.FindFirst(ClaimTypes.Surname)?.Value ?? string.Empty;

            user = new FinanceTrackerUser(firstName, lastName, email, oidcSub);
            dbContext.Users.Add(user);
        }
        else if (user.OpenIdConnectSubject == null)
        {
            // Link an existing local user account to Authelia upon their first SSO login
            user.SetOidcSubject(oidcSub);
            dbContext.Users.Update(user);
        }

        await dbContext.SaveChangesAsync();

        // Mutate the identity context to inject the ASP.NET Identity claims
        // This ensures UserManager<FinanceTrackerUser> natively recognizes the logged-in user
        var claimsIdentity = (ClaimsIdentity)principal.Identity!;
        claimsIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));

        IServiceProvider services = context.HttpContext.RequestServices;
        UserManager<FinanceTrackerUser> userManager = services.GetRequiredService<UserManager<FinanceTrackerUser>>();
        FinanceTrackerUser? identityUser = await userManager.FindByIdAsync(user.Id.ToString());
        if (identityUser is not null)
        {
            await services.GetRequiredService<AdminProvisioner>().GrantIfFirstUserAsync(identityUser);
        }

        foreach (string role in identityUser is null ? [] : await userManager.GetRolesAsync(identityUser))
        {
            claimsIdentity.AddClaim(new Claim(claimsIdentity.RoleClaimType, role));
        }
    }
}
