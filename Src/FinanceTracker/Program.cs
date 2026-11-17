using ApexCharts;
using FinanceTracker.Api;
using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Client;
using FinanceTracker.Components;
using FinanceTracker.Components.Account;
using FinanceTracker.Extensions;
using MudBlazor.Services;

namespace FinanceTracker;

/// <summary>
/// The web app. Serves the pages, the API and the sync hub, and runs the background jobs.
/// </summary>
public partial class Program
{
    /// <summary>
    /// Builds and runs the app.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.UseConfiguredCulture();
        builder.AddServiceDefaults();

        builder.Services.AddMudServices();
        builder.Services.AddApexCharts();

        // The client's pages run in the browser, the Identity pages render statically on the server
        builder.Services.AddRazorComponents()
            .AddInteractiveWebAssemblyComponents()
            .AddAuthenticationStateSerialization(options =>
                options.SerializationCallback = BrowserAuthenticationState.SerializeAsync);

        builder.Services.AddSignalR();
        builder.Services.AddCascadingValue(_ => new ApplicationState());

        builder.Services.AddSingleton<AppUpdate>();
        builder.Services.AddSecurityHeaders();
        builder.AddFinTrackRateLimiting();
        builder.AddFinTrackForwardedHeaders();

        builder.AddFinTrackAuthentication()
            .AddFinTrackPersistence()
            .AddFinTrackDataProtection()
            .AddOpenBanking()
            .AddBackgroundJobs()
            .AddFinTrackNotifications();

        // Only for data that isn't sensitive, fields running encryption should NOT enter the cache.
        builder.Services.AddHybridCache();

        builder.AddFinTrackClock();

        builder.Services.AddFinTrackOpenApi();

        // Source generated from the [Scoped]/[Singleton]/[Transient] attributes
        AddFinanceTrackerServices(builder.Services);
        AddFinanceTrackerValidators(builder.Services);
        AddFinanceTrackerExternalServices(builder.Services);

        WebApplication app = builder.Build();

        // First, so HTTPS redirection, the rate limits and logging see the client rather than the proxy
        app.UseFinTrackForwardedHeaders();

        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseExceptionHandler("/Error", true);
            // How long browsers keep to https is set in AddSecurityHeaders
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseSecurityHeaders();

        await app.MigrateDatabaseAsync();
        await app.EnsureAdminAsync();

        app.UseAppVersionHeader();
        app.MapDefaultEndpoints();
        app.UseCurrentUser();
        app.UseRateLimiter();
        app.MapOidcChallenge();

        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(ApplicationState).Assembly);

        app.MapAdditionalIdentityEndpoints();
        app.MapFinTrackApi();
        app.MapFinTrackOpenApi();

        await app.RunAsync();
    }
}
