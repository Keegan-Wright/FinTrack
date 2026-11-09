using System.Net;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications.Webhooks;

/// <summary>
/// Stands in for a webhook, keeps what was posted and answers with <paramref name="status"/>.
/// </summary>
internal sealed class RecordingWebhookHandler(HttpStatusCode status = HttpStatusCode.NoContent) : HttpMessageHandler
{
    public HttpRequestMessage? Request { get; private set; }

    public byte[] Body { get; private set; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Request = request;
        Body = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
        return new HttpResponseMessage(status);
    }
}
