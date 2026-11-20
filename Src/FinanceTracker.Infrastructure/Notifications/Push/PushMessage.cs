using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceTracker.Domain.Notifications;

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
    /// What's said in place of the message when amounts are kept off the lock screen.
    /// </summary>
    public const string AmountsHiddenBody = "Open FinTrack to see the amount.";

    /// <summary>
    /// The push for <paramref name="notification"/>. When it says amounts and the user keeps them off the lock screen,
    /// it shows <paramref name="titleWithoutAmounts"/> instead, and no message.
    /// </summary>
    /// <param name="notification">The notification.</param>
    /// <param name="titleWithoutAmounts">Its title without amounts, null when it says none.</param>
    /// <param name="showAmounts">Whether the user wants amounts on the lock screen.</param>
    public static PushMessage For(Notification notification, string? titleWithoutAmounts, bool showAmounts)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (titleWithoutAmounts is null || showAmounts)
        {
            return new PushMessage { Title = notification.Title, Body = notification.Message, Path = notification.Link };
        }

        return new PushMessage { Title = titleWithoutAmounts, Body = AmountsHiddenBody, Path = notification.Link };
    }

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
