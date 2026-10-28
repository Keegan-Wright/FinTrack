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

    public NetWorthSnapshot(Guid userId, DateOnly date, IReadOnlyCollection<BalanceSnapshot> balances) : base(userId)
    {
        Date = date;
        Total(balances);
    }

    public DateOnly Date { get; private set; }

    /// <summary>
    /// The accounts in credit.
    /// </summary>
    public decimal Assets { get; private set; }

    /// <summary>
    /// What's owed, as a positive amount. Overdrawn accounts for now, cards and debts later.
    /// </summary>
    public decimal Liabilities { get; private set; }

    public decimal NetWorth => Assets - Liabilities;

    /// <summary>
    /// At least one of the balances couldn't be refreshed when it was taken.
    /// </summary>
    public bool IsStale { get; private set; }

    /// <summary>
    /// Totals the day's balances, replacing the previous totals. Uses each account's current balance, not the
    /// available one, which can include an overdraft limit or pending payments.
    /// </summary>
    public void Total(IReadOnlyCollection<BalanceSnapshot> balances)
    {
        ArgumentNullException.ThrowIfNull(balances);

        decimal assets = 0;
        decimal liabilities = 0;
        bool isStale = false;

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
        }

        Assets = assets;
        Liabilities = liabilities;
        IsStale = isStale;
    }
}
