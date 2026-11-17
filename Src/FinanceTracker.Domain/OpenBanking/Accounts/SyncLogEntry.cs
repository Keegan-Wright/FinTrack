using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.OpenBanking.Accounts;

/// <summary>
/// Record of a completed sync for a <see cref="BankAccount"/>. Created via
/// <see cref="BankAccount.RecordSync"/>
/// </summary>
public sealed class SyncLogEntry : Entity
{
    [Obsolete("EF Use Only")]
    private SyncLogEntry()
    {
    }

    /// <param name="accountId">The account that was synced.</param>
    /// <param name="type">What was synced.</param>
    /// <param name="occurredAt">When the sync finished.</param>
    internal SyncLogEntry(Guid accountId, SyncType type, DateTime occurredAt)
    {
        AccountId = accountId;
        Type = type;
        OccurredAt = occurredAt;
    }

    /// <summary>
    /// The account that was synced.
    /// </summary>
    public Guid AccountId { get; private set; }

    /// <summary>
    /// What was synced.
    /// </summary>
    public SyncType Type { get; private set; }

    /// <summary>
    /// When the sync finished.
    /// </summary>
    public DateTime OccurredAt { get; private set; }
}
