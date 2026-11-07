namespace FinanceTracker.Contracts.Notifications;

/// <summary>
/// Something FinTrack told the user about.
/// </summary>
public sealed class Notification
{
    /// <summary>
    /// The notification's id, to mark it read.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// What it's about.
    /// </summary>
    public required NotificationKind Kind { get; init; }

    /// <summary>
    /// What happened, in a few words, e.g. <c>Everyday bank access ends in 7 days</c>.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// What it means and what to do about it.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Where in the app to deal with it, relative to the app, null for nowhere.
    /// </summary>
    public string? Link { get; init; }

    /// <summary>
    /// When it was raised, UTC.
    /// </summary>
    public required DateTime RaisedAt { get; init; }

    /// <summary>
    /// Whether the user has read it.
    /// </summary>
    public required bool IsRead { get; init; }
}
