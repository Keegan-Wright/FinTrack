namespace FinanceTracker.Domain.Common;

/// <summary>
/// Held for a bank account, such as its transactions and daily balances, so it's seen by whoever can see the
/// account, the owner and anyone it's shared with.
/// </summary>
public interface IBelongsToAccount
{
    Guid AccountId { get; }
}
