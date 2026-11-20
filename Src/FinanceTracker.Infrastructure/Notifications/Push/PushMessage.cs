using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.Notifications.Push;

/// <summary>
/// What a push shows, as the service worker reads it. A title, a line of text, and where tapping it goes.
/// </summary>
public sealed class PushMessage
{
    /// <summary>
    /// How it's written for the service worker, camel case.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// The notification's title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Its text.
    /// </summary>
    public required string Body { get; init; }

    /// <summary>
    /// Where in the app tapping it opens, relative to the app, e.g. <c>Notifications</c>. Null opens the app's start.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// The test sent from the Notifications page.
    /// </summary>
    public static PushMessage Test { get; } = new()
    {
        Title = "FinTrack",
        Body = "This is a test. Push notifications reach this device.",
        Path = "Notifications/Settings"
    };
}
