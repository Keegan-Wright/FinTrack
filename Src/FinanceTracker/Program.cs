using System.Globalization;
using System.Security.Claims;
using FinanceTracker.Ai;
using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.BackgroundJobs;
using FinanceTracker.Components;
using FinanceTracker.Components.Account;
using FinanceTracker.Configurations;
using FinanceTracker.Data;
using FinanceTracker.Data.Models;
using FinanceTracker.Shared.Setup;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
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

        SetCulture(builder);
        builder.AddServiceDefaults();
        AddBlazorServices(builder);
        AddIdentity(builder);
        AddRedis(builder);
        AddTickerQ(builder);
        AddFinanceTrackerServices(builder);

        WebApplication app = builder.Build();
        app.UseExceptionHandler("/Error", true);
        app.UseHttpsRedirection();

        await ExecuteDatabaseMigrationAsync(app);

        app.UseAntiforgery();
        app.UseTickerQ();
        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.UseOutputCache();
        app.MapAdditionalIdentityEndpoints();

        app.MapGet("/BackgroundProcessing",
            (IBackgroundSyncService syncService, CancellationToken ct) =>
            {
                return TypedResults.ServerSentEvents(syncService.GetSyncNotifications(ct),
                    FinanceTrackerConstants.BackgroundBankingSyncCompleteEventName);
            });

        await app.RunAsync();
    }

    private static void AddFinanceTrackerServices(WebApplicationBuilder builder)
    {
        builder.Services.AddSharedDependencies(builder.Configuration);
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
        builder.Services.AddHttpClient(FinanceTrackerConstants.OpenBankingHttpClientName);
        builder.Services.AddSingleton<IBackgroundSyncService, BackgroundSyncService>();
        builder.AddAiModule();
        AddFinanceTrackerServices(builder.Services);
        AddFinanceTrackerValidators(builder.Services);
        AddFinanceTrackerExternalServices(builder.Services);
    }

    private static void AddTickerQ(WebApplicationBuilder builder)
    {
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
    }

    private static void AddIdentity(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IdentityUserAccessor>();
        builder.Services.AddScoped<IdentityRedirectManager>();
        builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();
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
    }

    private static void AddBlazorServices(WebApplicationBuilder builder)
    {
        builder.Services.AddMudServices();
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.AddCascadingAuthenticationState();

        builder.Services.AddCascadingValue(_ => new ApplicationState());
    }

    private static void SetCulture(WebApplicationBuilder builder)
    {
        var culture = builder.Configuration["APP_CULTURE"];
        if (!string.IsNullOrEmpty(culture))
        {
            var cultureInfo = new CultureInfo(culture);
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
        }
    }

    private static void AddRedis(WebApplicationBuilder builder)
    {
        builder.AddRedisOutputCache(FinanceTrackerConstants.RedisConnectionName);
        builder.AddRedisDistributedCache(FinanceTrackerConstants.RedisConnectionName);
        builder.AddRedisDistributedCache(FinanceTrackerConstants.RedisConnectionName);
        builder.AddRedisClient(FinanceTrackerConstants.RedisConnectionName);
        builder.AddRedisDistributedCache(FinanceTrackerConstants.RedisConnectionName);
        builder.AddRedisOutputCache(FinanceTrackerConstants.RedisConnectionName);
    }

    private static async Task ExecuteDatabaseMigrationAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        FinanceTrackerContext db = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<FinanceTrackerContext>>()
            .CreateDbContextAsync();

        await db.Database.MigrateAsync();
    }
}
