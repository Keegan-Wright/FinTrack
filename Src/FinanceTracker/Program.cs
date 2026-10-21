using System.Globalization;
using System.Security.Claims;
using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.BackgroundJobs;
using FinanceTracker.Components;
using FinanceTracker.Components.Account;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Encryption;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MudBlazor.Services;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.Customizer;
using TickerQ.EntityFrameworkCore.DependencyInjection;
using TickerQ.Instrumentation.OpenTelemetry;

namespace FinanceTracker;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        string? culture = builder.Configuration[FinTrackEnvironment.Culture];
        if (!string.IsNullOrEmpty(culture))
        {
            CultureInfo cultureInfo = new(culture);
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
        }

        builder.AddServiceDefaults();
// Add MudBlazor services
        builder.Services.AddMudServices();

// Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

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




        string? oidcAuthority = builder.Configuration[FinTrackEnvironment.OidcAuthority];
        string? oidcClientId = builder.Configuration[FinTrackEnvironment.OidcClientId];
        string? oidcClientSecret = builder.Configuration[FinTrackEnvironment.OidcClientSecret];

        bool isOidcEnabled = !string.IsNullOrEmpty(oidcAuthority) &&
                             !string.IsNullOrEmpty(oidcClientId) &&
                             !string.IsNullOrEmpty(oidcClientSecret);



        if (isOidcEnabled)
        {
            authBuilder.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {

                options.Authority = oidcAuthority;
                options.ClientId = oidcClientId;
                options.ClientSecret = oidcClientSecret;

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
                    OnTokenValidated = async context =>
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
                    }
                };
            });
        }



        builder.Services.AddDbContextFactory<FinanceTrackerContext>((sp, options) =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString(FinTrackResources.Database), npgsqlDbContextOptionsBuilder =>
            {
                npgsqlDbContextOptionsBuilder.MigrationsAssembly("FinanceTracker.Infrastructure.Migrations");
                npgsqlDbContextOptionsBuilder.EnableRetryOnFailure();
                npgsqlDbContextOptionsBuilder.CommandTimeout(FinanceTrackerContext.CommandTimeoutSeconds);
            });
        });


        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        // Services act for ICurrentUser, set per request below, per circuit by the handler, per job by its scope
        builder.Services.AddCurrentUser();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<CircuitHandler, CurrentUserCircuitHandler>());


        builder.Services.AddIdentityCore<FinanceTrackerUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.SignIn.RequireConfirmedEmail = false;
                options.SignIn.RequireConfirmedPhoneNumber = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<FinanceTrackerContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        builder.Services.AddSingleton<IEmailSender<FinanceTrackerUser>, IdentityNoOpEmailSender>();
        builder.AddRedisClient(FinTrackResources.Redis);
        builder.AddRedisDistributedCache(FinTrackResources.Redis);
        builder.AddRedisOutputCache(FinTrackResources.Redis);

        TrueLayerOpenBankingConfiguration trueLayerConfig = new()
        {
            BaseAuthUrl = builder.Configuration.GetValue<Uri>(FinTrackEnvironment.TrueLayerBaseAuthUrl)!,
            BaseDataUrl = builder.Configuration.GetValue<Uri>(FinTrackEnvironment.TrueLayerBaseDataUrl)!,
            AuthRedirectUrl = builder.Configuration.GetValue<Uri>(FinTrackEnvironment.TrueLayerAuthRedirectUrl)!,
            ClientId = builder.Configuration.GetValue<string>(FinTrackEnvironment.TrueLayerClientId)!,
            ClientSecret = builder.Configuration.GetValue<Guid>(FinTrackEnvironment.TrueLayerClientSecret),
            PublicIpAddress = builder.Configuration.GetValue<string>(FinTrackEnvironment.PublicIpAddress) ?? string.Empty
        };

        builder.Services.AddSingleton(trueLayerConfig);

        EncryptionConfiguration encryptionConfig = EncryptionConfiguration.Parse(
            builder.Configuration.GetValue<string>(FinTrackEnvironment.EncryptionKeys)
                ?? throw new InvalidOperationException($"{FinTrackEnvironment.EncryptionKeys} isn't configured."),
            builder.Configuration.GetValue<int>(FinTrackEnvironment.EncryptionCurrentKeyId));

        builder.Services.AddSingleton(encryptionConfig);

        builder.Services.AddTickerQ(options =>
        {
            options.AddOpenTelemetryInstrumentation();

            options.ConfigureScheduler(schedulerOptions => { schedulerOptions.SchedulerTimeZone = TimeZoneInfo.Utc; });

            options.AddOperationalStore(efOptions =>
            {
                efOptions.UseApplicationDbContext<FinanceTrackerContext>(ConfigurationType.IgnoreModelCustomizer);
            });

            options.IgnoreSeedDefinedCronTickers();
        });

        builder.Services.MapTicker<SyncAllOpenBankingDetailsAsync>();
        builder.Services.MapTicker<SyncUserOpenBankingDetailsAsync, SyncUserOpenBankingDetailsRequest>();
        builder.Services.MapTicker<MonitorOpenBankingTokenExpirationAsync>();


        builder.Services.AddCascadingValue(_ => new ApplicationState());


        builder.Services.AddHttpClient(TrueLayerOpenBankingApiService.HttpClientName);

        builder.Services.AddSingleton<ISyncNotifier, SyncNotifier>();
        builder.Services.AddSingleton(TimeProvider.System);

        AddFinanceTrackerServices(builder.Services);
        AddFinanceTrackerValidators(builder.Services);
        AddFinanceTrackerExternalServices(builder.Services);

        WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();
        }
        else
        {
            app.UseExceptionHandler("/Error", true);
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        await ExecuteDatabaseMigrationAsync(app);

        app.MapDefaultEndpoints();

        // Authentication has already run, so the request's user is known. Covers static and prerendered pages and
        // endpoints, interactive circuits are handled by CurrentUserCircuitHandler
        app.Use((context, next) =>
        {
            context.RequestServices.GetRequiredService<CurrentUser>().Set(context.User);
            return next(context);
        });

        if (isOidcEnabled)
        {
            app.MapGet("/auth/oidc", async (HttpContext context) =>
            {
                await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties
                {
                    RedirectUri = "/"
                });
            });

        }

        app.UseAntiforgery();
        app.UseTickerQ();
        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.UseOutputCache();
        app.MapAdditionalIdentityEndpoints();

        await app.RunAsync();
    }

    private static async Task ExecuteDatabaseMigrationAsync(WebApplication app)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();

        await using (FinanceTrackerContext db = await scope.ServiceProvider
                         .GetRequiredService<IDbContextFactory<FinanceTrackerContext>>()
                         .CreateDbContextAsync())
        {
            await db.Database.MigrateAsync();
        }

        // Anything still under an older key is re-encrypted before the app serves requests
        await ActivatorUtilities.CreateInstance<EncryptionKeyRotator>(scope.ServiceProvider)
            .RotateAsync(CancellationToken.None);
    }
}
