using System.Net;

namespace FinanceTracker.Tests.Unit.Client.Api;

/// <summary>
/// The server, answering every request with <paramref name="status"/> and <see cref="Headers"/>.
/// </summary>
internal sealed class AnsweringHandler(HttpStatusCode status) : HttpMessageHandler
{
    public Dictionary<string, string> Headers { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response = new(status);
        foreach ((string name, string value) in Headers)
        {
            response.Headers.Add(name, value);
        }

        return Task.FromResult(response);
    }
}
