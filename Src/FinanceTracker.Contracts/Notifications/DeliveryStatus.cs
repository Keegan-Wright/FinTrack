namespace FinanceTracker.Contracts.Notifications;

/// <summary>
///  Last try at sending to a channel went, e.g. the last email.
/// </summary>
public sealed class DeliveryStatus
{
    /// <summary>
    /// When it tried, UTC.
    /// </summary>
    public required DateTime At { get; init; }

    /// <summary>
    /// Why it couldn't send, in words for the user, null when it sent.
    /// </summary>
    public string? Failure { get; init; }

    /// <summary>
    /// Whether it sent.
    /// </summary>
    public bool Sent => Failure is null;
}
