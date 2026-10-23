using FinanceTracker.Api;
using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Client;
using FinanceTracker.Components;
using FinanceTracker.Components.Account;
using FinanceTracker.Extensions;
using MudBlazor.Services;
using TickerQ.DependencyInjection;

namespace FinanceTracker;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.UseConfiguredCulture();
        builder.AddServiceDefaults();

        builder.Services.AddMudServices();

        // The client's pages run in the browser, the Identity pages render statically on the server
        builder.Services.AddRazorComponents()
            .AddInteractiveWebAssemblyComponents()
            .AddAuthenticationStateSerialization(options =>
                options.SerializationCallback = BrowserAuthenticationState.SerializeAsync);

        builder.Services.AddSignalR();
        builder.Services.AddCascadingValue(_ => new ApplicationState());

        builder.AddFinTrackAuthentication()
            .AddFinTrackPersistence()
            .AddFinTrackDataProtection()
            .AddOpenBanking()
            .AddBackgroundJobs();

        // Only for data that isn't sensitive, fields running encryption should NOT enter the cache.
        builder.Services.AddHybridCache();

        builder.Services.AddSingleton(TimeProvider.System);

        // Source generated from the [Scoped]/[Singleton]/[Transient] attributes
        AddFinanceTrackerServices(builder.Services);
        AddFinanceTrackerValidators(builder.Services);
        AddFinanceTrackerExternalServices(builder.Services);

        WebApplication app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseExceptionHandler("/Error", true);
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        await app.MigrateDatabaseAsync();
        await app.EnsureAdminAsync();

        app.MapDefaultEndpoints();
        app.UseCurrentUser();
        app.MapOidcChallenge();

        app.UseAntiforgery();
        app.UseTickerQ();
        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(ApplicationState).Assembly);

        app.MapAdditionalIdentityEndpoints();
        app.MapFinTrackApi();

        await app.RunAsync();
    }
}
