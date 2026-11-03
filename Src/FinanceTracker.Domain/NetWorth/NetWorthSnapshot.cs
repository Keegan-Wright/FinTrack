using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Balances;

namespace FinanceTracker.Domain.NetWorth;

/// <summary>
/// A user's net worth at the end of a day, totalled from that day's <see cref="BalanceSnapshot"/>s. One per user per
/// day. Stored rather than recalculated, so a day's figure stays the same if an account is later removed.
/// </summary>
public sealed class NetWorthSnapshot : AggregateRoot
{
    [Obsolete("EF Use Only")]
    private NetWorthSnapshot()
    {
    }

    /// <param name="debts">What the debts not linked to an account owed that day, see <see cref="Total"/>.</param>
    public NetWorthSnapshot(Guid userId, DateOnly date, IReadOnlyCollection<BalanceSnapshot> balances,
        decimal debts = 0) : base(userId)
    {
        Date = date;
        Total(balances, debts);
    }

    public DateOnly Date { get; private set; }

    /// <summary>
    /// The accounts in credit.
    /// </summary>
    public decimal Assets { get; private set; }

    /// <summary>
    /// What's owed, as a positive amount. Overdrawn accounts, cards and debts.
    /// </summary>
    public decimal Liabilities { get; private set; }

    public decimal NetWorth => Assets - Liabilities;

    /// <summary>
    /// At least one of the balances couldn't be refreshed when it was taken.
    /// </summary>
    public bool IsStale { get; private set; }

    /// <summary>
    /// At least one of the balances was worked out from transactions rather than synced, so it's approximate.
    /// </summary>
    public bool IsDerived { get; private set; }

    /// <summary>
    /// Totals the day's balances, replacing the previous totals. Uses each account's current balance, not the
    /// available one, which can include an overdraft limit or pending payments.
    /// </summary>
    /// <param name="debts">What the debts not linked to an account owed that day, as a positive amount. A debt linked
    /// to an account is already in its balance.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="debts"/> is negative.</exception>
    public void Total(IReadOnlyCollection<BalanceSnapshot> balances, decimal debts = 0)
    {
        ArgumentNullException.ThrowIfNull(balances);
        ArgumentOutOfRangeException.ThrowIfNegative(debts);

        decimal assets = 0;
        decimal liabilities = debts;
        bool isStale = false;
        bool isDerived = false;

        foreach (BalanceSnapshot snapshot in balances)
        {
            if (snapshot.Date != Date)
            {
                throw new ArgumentException($"The balance for {snapshot.Date} isn't for {Date}.", nameof(balances));
            }

            decimal current = snapshot.Balance.Current;
            if (current >= 0)
            {
                assets += current;
            }
            else
            {
                liabilities -= current;
            }

            if (snapshot.IsStale)
            {
                isStale = true;
            }

            if (snapshot.Source == BalanceSnapshotSource.Derived)
            {
                isDerived = true;
            }
        }

        Assets = assets;
        Liabilities = liabilities;
        IsStale = isStale;
        IsDerived = isDerived;
    }
}
