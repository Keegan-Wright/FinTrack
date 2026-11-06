using System.Net;

namespace FinanceTracker.Tests.Unit.Client.Api;

/// <summary>
/// A sync hub that can't be reached, e.g. a server restarting, answering every try to connect with 503 and counting
/// them.
/// </summary>
internal sealed class RefusingHubHandler : HttpMessageHandler
{
    private int _tries;

    public int Tries => Volatile.Read(ref _tries);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _tries);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
}
