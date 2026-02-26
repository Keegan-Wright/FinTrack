using System.Globalization;
using System.Security.Claims;
using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Components;
using FinanceTracker.Components.Account;
using FinanceTracker.Configurations;
using FinanceTracker.Data;
using FinanceTracker.Data.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
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

        var culture = builder.Configuration["APP_CULTURE"];
        if (!string.IsNullOrEmpty(culture))
        {
            var cultureInfo = new CultureInfo(culture);
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

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        builder.Services.AddDbContextFactory<FinanceTrackerContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("FinTrackDb"), npgsqlDbContextOptionsBuilder =>
            {
                npgsqlDbContextOptionsBuilder.MigrationsAssembly("FinanceTracker.Data.Migrations");
                npgsqlDbContextOptionsBuilder.EnableRetryOnFailure();
                npgsqlDbContextOptionsBuilder.CommandTimeout(0);
            }));

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
        builder.AddRedisClient("financeTrackerRedis");
        builder.AddRedisDistributedCache("financeTrackerRedis");
        builder.AddRedisOutputCache("financeTrackerRedis");

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

        EncryptionSettings encryptionConfig = new()
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
                efOptions.UseApplicationDbContext<FinanceTrackerContext>(ConfigurationType.UseModelCustomizer);
            });
        });



        builder.Services.AddCascadingValue(_ => new ApplicationState());

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

        using IServiceScope scope = app.Services.CreateScope();
        FinanceTrackerContext db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<FinanceTrackerContext>>()
            .CreateDbContextAsync();
        await db.Database.MigrateAsync();


        app.UseAntiforgery();
        app.UseTickerQ();
        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.UseOutputCache();
        app.MapAdditionalIdentityEndpoints();


        await app.RunAsync();
    }
}
