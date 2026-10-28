using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Accounts;

namespace FinanceTracker.Domain.OpenBanking.Balances;

/// <summary>
/// An account's balance for a day, from the day's latest sync, so its closing balance once the day is over. One per
/// account per day. Taken from a synced account through <see cref="BankAccount.SnapshotBalance"/>, then kept up to
/// date through the day with <see cref="Refresh"/>.
/// </summary>
public sealed class BalanceSnapshot : AggregateRoot
{
    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private BalanceSnapshot()
    {
    }
#pragma warning restore CS8618

    public BalanceSnapshot(Guid userId, Guid accountId, DateOnly date, AccountBalance balance,
        BalanceSnapshotSource source, bool isStale) : base(userId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(accountId, Guid.Empty);

        AccountId = accountId;
        Date = date;
        Replace(balance, source, isStale);
    }

    public Guid AccountId { get; private set; }

    /// <summary>
    /// The day this is the balance for.
    /// </summary>
    public DateOnly Date { get; private set; }

    /// <summary>
    /// The balance as the bank reported it. Its <see cref="AccountBalance.AsOf"/> can be before the day ended.
    /// </summary>
    public AccountBalance Balance { get; private set; }

    public BalanceSnapshotSource Source { get; private set; }

    /// <summary>
    /// The balance couldn't be synced when the snapshot was taken, so it's the last one stored.
    /// </summary>
    public bool IsStale { get; private set; }

    /// <summary>
    /// Takes the account's balance after a later sync the same day. A balance that didn't sync this time is left
    /// alone, so a refused call never replaces one that synced earlier, and a stale snapshot stays stale until one
    /// does.
    /// </summary>
    /// <param name="synced">Whether the account's balance synced this time.</param>
    /// <exception cref="ArgumentException">The account isn't this snapshot's, or has no balance.</exception>
    public void Refresh(BankAccount account, bool synced)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (account.Id != AccountId)
        {
            throw new ArgumentException($"Account {account.Id} isn't the one this snapshot is for.", nameof(account));
        }

        if (!synced)
        {
            return;
        }

        if (account.Balance is null)
        {
            throw new ArgumentException($"Account {account.Id} synced without a balance.", nameof(account));
        }

        // A copy, EF won't share an owned instance between two owners
        Replace(account.Balance with { }, BalanceSnapshotSource.Synced, isStale: false);
    }

    [MemberNotNull(nameof(Balance))]
    private void Replace(AccountBalance balance, BalanceSnapshotSource source, bool isStale)
    {
        ArgumentNullException.ThrowIfNull(balance);

        Balance = balance;
        Source = source;
        IsStale = isStale;
    }
}
