using System.Globalization;
using System.Security.Claims;
using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.BackgroundJobs;
using FinanceTracker.Components;
using FinanceTracker.Components.Account;
using FinanceTracker.Domain;
using FinanceTracker.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

        string? culture = builder.Configuration["APP_CULTURE"];
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

        builder.AddRedisOutputCache("FinTrack-Redis");
        builder.AddRedisDistributedCache("FinTrack-Redis");

        AuthenticationBuilder authBuilder = builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        });

        authBuilder.AddIdentityCookies();




        var oidcAuthority = builder.Configuration["OIDC_Authority"];
        var oidcClientId = builder.Configuration["OIDC_ClientId"];
        var oidcClientSecret = builder.Configuration["OIDC_ClientSecret"];

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
                        var oidcSub = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        var email = context.Principal?.FindFirst(ClaimTypes.Email)?.Value;


                        if (string.IsNullOrEmpty(oidcSub)) return;

                        var factory = context.HttpContext.RequestServices
                            .GetRequiredService<IDbContextFactory<FinanceTrackerContext>>();

                        await using var dbContext = await factory.CreateDbContextAsync();

                        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.OpenIdConnectSubject == oidcSub)
                                   ?? await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);

                        if (user == null)
                        {
                            var firstName = context.Principal?.FindFirst(ClaimTypes.GivenName)?.Value;
                            var lastName = context.Principal?.FindFirst(ClaimTypes.Surname)?.Value;

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
                        var claimsIdentity = (ClaimsIdentity)context.Principal.Identity!;
                        claimsIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
                    }
                };
            });
        }



        builder.Services.AddDbContextFactory<FinanceTrackerContext>((sp, options) =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("FinTrackDb"), npgsqlDbContextOptionsBuilder =>
            {
                npgsqlDbContextOptionsBuilder.MigrationsAssembly("FinanceTracker.Infrastructure.Migrations");
                npgsqlDbContextOptionsBuilder.EnableRetryOnFailure();
                npgsqlDbContextOptionsBuilder.CommandTimeout(0);
            });
        });


        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ClaimsPrincipal?>(s =>
            s.GetService<IHttpContextAccessor>()?.HttpContext?.User ?? null);


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
        builder.AddRedisClient("FinTrack-Redis");
        builder.AddRedisDistributedCache("FinTrack-Redis");
        builder.AddRedisOutputCache("FinTrack-Redis");

        TrueLayerOpenBankingConfiguration trueLayerConfig = new()
        {
            BaseAuthUrl = builder.Configuration.GetValue<Uri>("OPEN_BANKING_TRUELAYER_BASE_AUTH_URL")!,
            BaseDataUrl = builder.Configuration.GetValue<Uri>("OPEN_BANKING_TRUELAYER_BASE_DATA_URL")!,
            AuthRedirectUrl = builder.Configuration.GetValue<Uri>("OPEN_BANKING_TRUELAYER_AUTH_REDIRECT_URL")!,
            ClientId = builder.Configuration.GetValue<string>("OPEN_BANKING_TRUELAYER_CLIENT_ID")!,
            ClientSecret = builder.Configuration.GetValue<Guid>("OPEN_BANKING_TRUELAYER_CLIENT_SECRET"),
            PublicIpAddress = builder.Configuration.GetValue<string>("OPEN_BANKING_PUBLIC_IP_ADDRESS")
        };

        builder.Services.AddSingleton(trueLayerConfig);

        EncryptionConfiguration encryptionConfig = new()
        {
            SymmetricKey = builder.Configuration.GetValue<string>("ENCRYPTION_KEY")!,
            SymmetricSalt = builder.Configuration.GetValue<string>("ENCRYPTION_SALT")!,
            Iterations = builder.Configuration.GetValue<int>("ENCRYPTION_ITERATIONS")
        };

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


        builder.Services.AddCascadingValue(_ => new ApplicationState());


        builder.Services.AddHttpClient("OpenBankingClient");

        builder.Services.AddSingleton<IBackgroundSyncService, BackgroundSyncService>();

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


        app.MapGet("/BackgroundProcessing",
            (IBackgroundSyncService syncService, CancellationToken ct) =>
            {
                return TypedResults.ServerSentEvents(syncService.GetSyncNotifications(ct),
                    "BackgroundBankingSyncComplete");
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

        FinanceTrackerContext db = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<FinanceTrackerContext>>()
            .CreateDbContextAsync();

        await db.Database.MigrateAsync();
    }
}
