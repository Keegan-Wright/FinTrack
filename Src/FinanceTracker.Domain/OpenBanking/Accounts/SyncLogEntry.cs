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

    internal SyncLogEntry(Guid accountId, SyncType type, DateTime occurredAt)
    {
        AccountId = accountId;
        Type = type;
        OccurredAt = occurredAt;
    }

    public Guid AccountId { get; private set; }

    public SyncType Type { get; private set; }

    public DateTime OccurredAt { get; private set; }
}
