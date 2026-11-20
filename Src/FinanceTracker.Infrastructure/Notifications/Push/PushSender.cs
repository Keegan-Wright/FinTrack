using System.Buffers.Text;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Infrastructure.Notifications.Push;

/// <summary>
/// <see cref="IPushSender"/> over the app's client factory, a fresh client each push from the
/// <see cref="ClientName"/> client. That client goes through the webhooks' handler, so a subscription made up to
/// point inside the network is refused like a webhook would be, and doesn't retry, see <c>AddPushClient</c>.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Singleton<IPushSender>]
public sealed class PushSender : IPushSender
{
    /// <summary>
    /// The factory's client for push services.
    /// </summary>
    public const string ClientName = "push";

    /// <summary>
    /// How long the push service keeps a message for a browser that's offline, a day. A notice older than that has
    /// been seen under the bell or by email.
    /// </summary>
    public static readonly TimeSpan TimeToLive = TimeSpan.FromDays(1);

    /// <summary>
    /// How long to wait for the push service to answer, short enough that a test from the Notifications page doesn't
    /// hang.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IVapidKeyStore _vapidKeyStore;
    private readonly PublicAddress _publicAddress;
    private readonly TimeProvider _timeProvider;

    /// <param name="httpClientFactory">Makes the push client.</param>
    /// <param name="vapidKeyStore">The server's key pair, which signs each push.</param>
    /// <param name="publicAddress">The app's address, given to push services as who sends.</param>
    /// <param name="timeProvider">The clock.</param>
    public PushSender(IHttpClientFactory httpClientFactory, IVapidKeyStore vapidKeyStore,
        PublicAddress publicAddress, TimeProvider timeProvider)
    {
        _httpClientFactory = httpClientFactory;
        _vapidKeyStore = vapidKeyStore;
        _publicAddress = publicAddress;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task<PushOutcome> SendAsync(PushDevice device, PushMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(message);

        Uri endpoint = new(device.Endpoint, UriKind.Absolute);
        VapidKeys keys = await _vapidKeyStore.GetAsync(cancellationToken);

        byte[] body = WebPushEncryption.Encrypt(JsonSerializer.SerializeToUtf8Bytes(message, PushMessage.JsonOptions),
            Base64Url.DecodeFromChars(device.PublicKey), Base64Url.DecodeFromChars(device.AuthSecret));

        using HttpRequestMessage request = new(HttpMethod.Post, endpoint);
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Content.Headers.ContentEncoding.Add("aes128gcm");

        request.Headers.Authorization = new AuthenticationHeaderValue(VapidAuthorization.Scheme,
            VapidAuthorization.HeaderValue(endpoint, keys, _publicAddress.Url, _timeProvider.GetUtcNow()));

        request.Headers.Add("TTL", ((int)TimeToLive.TotalSeconds).ToString(CultureInfo.InvariantCulture));
        request.Headers.Add("Urgency", "normal");

        using HttpClient client = _httpClientFactory.CreateClient(ClientName);
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return PushOutcome.Gone;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new PushRejectedException(response.StatusCode);
        }

        return PushOutcome.Delivered;
    }
}
