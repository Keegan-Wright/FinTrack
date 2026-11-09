namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications.Webhooks;

/// <summary>
/// A client factory whose clients all go through <paramref name="handler"/>, e.g. one recording what's posted.
/// </summary>
internal sealed class HandlerClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
