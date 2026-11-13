namespace FinanceTracker.Domain.Sharing;

/// <summary>
/// Where a <see cref="UserLink"/> is in its life.
/// </summary>
public enum UserLinkStatus
{
    /// <summary>
    /// Sent, waiting for the person invited to answer.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Accepted, the two are linked and can share with each other.
    /// </summary>
    Accepted = 1,

    /// <summary>
    /// The person invited said no.
    /// </summary>
    Declined = 2,

    /// <summary>
    /// Withdrawn before it was answered, or ended after it was accepted, by either of them.
    /// </summary>
    Ended = 3
}
