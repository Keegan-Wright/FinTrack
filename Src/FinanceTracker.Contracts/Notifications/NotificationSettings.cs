namespace FinanceTracker.Contracts.Notifications;

/// <summary>
/// Which notifications the user also has emailed, every one shows under the bell.
/// </summary>
public sealed class NotificationSettings
{
    /// <summary>
    /// The kinds emailed.
    /// </summary>
    public required IReadOnlyList<NotificationKind> Emailed { get; init; }

    /// <summary>
    /// Where they're emailed, the account's address, null when it has none.
    /// </summary>
    public string? EmailAddress { get; init; }

    /// <summary>
    /// Whether this FinTrack can send email at all, set up by whoever runs it.
    /// </summary>
    public required bool EmailAvailable { get; init; }
}
