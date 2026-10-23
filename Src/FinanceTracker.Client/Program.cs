using FinanceTracker.Client.Api;
using FinanceTracker.Client.Extensions;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Logging;
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
public static class Program
{
    public static async Task Main(string[] args)
    {
        WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);

        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.Services.AddMudServices();
        builder.Services.AddCascadingValue(_ => new ApplicationState());
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddAuthorizationCore();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddAuthenticationStateDeserialization();

        builder.Services.AddContractValidators();
        builder.Services.AddFinTrackApiClients(new Uri(builder.HostEnvironment.BaseAddress));

        await builder.Build().RunAsync();
    }
}
