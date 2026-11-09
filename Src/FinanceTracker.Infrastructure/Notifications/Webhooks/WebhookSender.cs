using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Infrastructure.Notifications.Webhooks;

/// <summary>
/// <see cref="IWebhookSender"/> over one long lived client on <see cref="WebhookHttp"/>'s handler. Not one from
/// the app's client factory. Those retry, which would keep a test from the page waiting, and the receiver may not
/// expect a repeat.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Singleton<IWebhookSender>]
public sealed class WebhookSender : IWebhookSender, IDisposable
{
    public const string EventHeader = "X-FinTrack-Event";
    public const string DeliveryHeader = "X-FinTrack-Delivery";
    public const string TimestampHeader = "X-FinTrack-Timestamp";
    public const string SignatureHeader = "X-FinTrack-Signature";

    /// <summary>
    /// How long to wait for the webhook to answer, short enough that a test from the Notifications page doesn't hang.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _client;
    private readonly TimeProvider _timeProvider;

    public WebhookSender(WebhookSettings settings, TimeProvider timeProvider)
        : this(WebhookHttp.CreateHandler(settings), timeProvider)
    {
    }

    /// <param name="handler">What posts go through, the sender disposes of it.</param>
    internal WebhookSender(HttpMessageHandler handler, TimeProvider timeProvider)
    {
        _client = new HttpClient(handler) { Timeout = Timeout };
        _timeProvider = timeProvider;
    }

    public async Task SendAsync(string url, string secret, WebhookPayload payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(payload, WebhookPayload.JsonOptions);
        long timestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds();

        using HttpRequestMessage request = new(HttpMethod.Post, url);
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        request.Headers.UserAgent.ParseAdd("FinTrack-Webhook/1");
        request.Headers.Add(EventHeader, payload.Event);
        request.Headers.Add(DeliveryHeader, payload.Id.ToString());
        request.Headers.Add(TimestampHeader, timestamp.ToString(CultureInfo.InvariantCulture));
        request.Headers.Add(SignatureHeader, WebhookSignature.Sign(secret, timestamp, body));

        using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new WebhookRejectedException(response.StatusCode);
        }
    }

    public void Dispose() => _client.Dispose();
}
