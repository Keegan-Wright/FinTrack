using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceTracker.Domain.Notifications;

namespace FinanceTracker.Infrastructure.Notifications.Webhooks;

/// <summary>
/// The JSON body of a webhook post, camel case, e.g.
/// <c>{"id":"…","event":"notification","kind":"consentEndingSoon","title":"…","message":"…","link":"https://…","raisedAt":"2026-11-07T09:00:00Z"}</c>.
/// Whatever receives it may rely on these names, so they don't change.
/// </summary>
/// <param name="Id">The notification's id, the same if it's ever posted again, so a receiver can ignore repeats.</param>
/// <param name="Event"><see cref="NotificationEvent"/>, or <see cref="TestEvent"/> for a test.</param>
/// <param name="Kind">What it's about, null for a test.</param>
/// <param name="Link">Where in FinTrack it's dealt with, null when FinTrack's address isn't known.</param>
/// <param name="RaisedAt">When it was raised, UTC.</param>
public sealed record WebhookPayload(
    Guid Id,
    string Event,
    NotificationKind? Kind,
    string Title,
    string Message,
    Uri? Link,
    DateTime RaisedAt)
{
    public const string NotificationEvent = "notification";

    public const string TestEvent = "test";

    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static WebhookPayload For(Notification notification, PublicAddress publicAddress)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(publicAddress);

        return new WebhookPayload(notification.Id, NotificationEvent, notification.Kind, notification.Title,
            notification.Message, publicAddress.LinkTo(notification.Link),
            DateTime.SpecifyKind(notification.Created, DateTimeKind.Utc));
    }

    public static WebhookPayload Test(DateTime now, PublicAddress publicAddress)
    {
        ArgumentNullException.ThrowIfNull(publicAddress);

        return new WebhookPayload(Guid.NewGuid(), TestEvent, null, "FinTrack test",
            "Webhooks from FinTrack work.", publicAddress.LinkTo(Email.NotificationEmails.SettingsPath),
            DateTime.SpecifyKind(now, DateTimeKind.Utc));
    }
}
