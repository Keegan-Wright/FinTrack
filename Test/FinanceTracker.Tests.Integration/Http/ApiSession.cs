using FinanceTracker.Client.Api;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Integration.Http;

/// <summary>
/// The front end's API clients, registered as the browser app registers them, signed in with a
/// <see cref="FinTrackClient"/>'s cookies. Resolve a <c>Contracts.Services</c> interface to call the API as that user.
/// </summary>
public sealed class ApiSession : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly AsyncServiceScope _scope;

    public ApiSession(FinTrackClient client)
    {
        ServiceCollection services = new();

        Client.Program.AddFinanceTrackerServices(services);
        services.AddFinTrackApiClients(client.BaseAddress, hub =>
            {
                hub.Cookies = client.Cookies;
                // The development certificate may not be trusted, long polling keeps to plain HTTP requests
                hub.Transports = HttpTransportType.LongPolling;
                hub.HttpMessageHandlerFactory = _ => TrustingHandler(client);
            })
            .ConfigurePrimaryHttpMessageHandler(() => TrustingHandler(client));

        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _services.CreateAsyncScope();
    }

    public T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _services.DisposeAsync();
    }

    private static HttpClientHandler TrustingHandler(FinTrackClient client) => new()
    {
        CookieContainer = client.Cookies,
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    };
}
