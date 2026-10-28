using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.OpenBanking.Transfers;

/// <summary>
/// When a user's transfers were last matched, so the next run only looks at what's changed since. One per user.
/// </summary>
public sealed class TransferMatchCheckpoint : AggregateRoot
{
    [Obsolete("EF Use Only")]
    private TransferMatchCheckpoint()
    {
    }

    public TransferMatchCheckpoint(Guid userId, DateTime matchedFrom) : base(userId) => MatchedFrom = matchedFrom;

    /// <summary>
    /// When the last run started loading. Anything created or updated after this hasn't been matched yet. Transfer
    /// decisions don't count as updates, they're stored alongside a transaction rather than changing its own values.
    /// </summary>
    public DateTime MatchedFrom { get; private set; }

    public void Record(DateTime matchedFrom) => MatchedFrom = matchedFrom;
}
