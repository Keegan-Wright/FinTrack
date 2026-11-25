using ApexCharts;
#if FINTRACK_AI
using FinanceTracker.Ai.Contracts;
using FinanceTracker.Client.Ai;
using Microsoft.AspNetCore.Components;
#endif
using FinanceTracker.Client.Api;
using FinanceTracker.Client.Common;
using FinanceTracker.Client.Diagnostics;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

namespace FinanceTracker.Client;

/// <summary>
/// The front end in the browser. Pages call the server through the API clients and act as the user signed in to the
/// server, whose authentication state the server hands over with the prerendered page.
/// </summary>
/// <remarks>
/// The culture and local time zone are the server's, set by <c>App.razor</c> before the runtime starts, so dates and
/// amounts read the same in the browser as on the server.
/// </remarks>
public static partial class Program
{
    /// <summary>
    /// Builds and runs the app in the browser.
    /// </summary>
    /// <param name="args">The arguments the browser starts it with.</param>
    public static async Task Main(string[] args)
    {
        WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);

        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // Errors in the browser are sent to the server's logs too, see ClientErrorLoggerProvider
        builder.Services.AddSingleton<ClientErrorReporter>();
        builder.Services.AddSingleton<ILoggerProvider, ClientErrorLoggerProvider>();

        builder.Services.AddMudServices();
        builder.Services.AddApexCharts();
        builder.Services.AddCascadingValue(_ => new ApplicationState());
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddAuthorizationCore();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddAuthenticationStateDeserialization();

        AddFinanceTrackerServices(builder.Services);
        AddFinanceTrackerValidators(builder.Services);
        AddFinanceTrackerExternalServices(builder.Services);


        builder.Services.AddSingleton<AppUpdate>();
#if FINTRACK_AI
        // One per browser tab, shared by the startup below, the router and the layout
        builder.Services.AddSingleton<AiModuleLoader>();
        builder.Services.AddSingleton<IAiService, AiApiClient>();
        builder.Services.AddSingleton<AiStatusState>();
#endif
        builder.Services.AddTransient<SignedOutHandler>();
        builder.Services.AddTransient<ServerVersionHandler>();
        builder.Services.AddFinTrackServerConnections(new Uri(builder.HostEnvironment.BaseAddress))
            .AddHttpMessageHandler<SignedOutHandler>()
            .AddHttpMessageHandler<ServerVersionHandler>();

        WebAssemblyHost host = builder.Build();
#if FINTRACK_AI
        // Opening an AI page loads its assembly before the app starts, so the server's page stays on screen rather
        // than going blank while the router waits for it
        await host.Services.GetRequiredService<AiModuleLoader>()
            .LoadIfOpeningModulePageAsync(host.Services.GetRequiredService<NavigationManager>());
#endif
        await host.RunAsync();
    }
}
