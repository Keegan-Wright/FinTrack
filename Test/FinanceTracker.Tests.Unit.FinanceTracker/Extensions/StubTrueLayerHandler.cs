namespace FinanceTracker.Tests.Unit.FinanceTracker.Extensions;

/// <summary>
/// Stands in for TrueLayer at the end of a client's handlers, counting the requests that get through to it.
/// </summary>
/// <param name="respond">Answers each request, given how many have arrived including it.</param>
internal sealed class StubTrueLayerHandler(Func<int, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken) => respond(Interlocked.Increment(ref _calls));
}
