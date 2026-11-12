    using FinanceTracker.Contracts.NetWorth;
using FinanceTracker.Domain.OpenBanking.Balances;

namespace FinanceTracker.Infrastructure.NetWorth;

/// <summary>
/// One day's net worth being added up, from the user's own total with other accounts' balances added or taken out.
/// </summary>
internal sealed class NetWorthTally
{
    private decimal _assets;
    private decimal _liabilities;
    private bool _isStale;
    private bool _isDerived;

    /// <param name="own">The user's own total, the last known by the day, null before they have one.</param>
    public NetWorthTally(NetWorthPoint? own)
    {
        _assets = own?.Assets ?? 0;
        _liabilities = own?.Liabilities ?? 0;
        _isStale = own?.IsStale ?? false;
        _isDerived = own?.IsDerived ?? false;
    }

    /// <summary>
    /// Counts an account's balance in, an asset when it's in credit and a liability when it's owed.
    /// </summary>
    public void Add(BalanceSnapshot balance)
    {
        Count(balance.Balance.Current, sign: 1);
        _isStale |= balance.IsStale;
        _isDerived |= balance.Source == BalanceSnapshotSource.Derived;
    }

    /// <summary>
    /// Counts in what a debt not linked to an account owed, a liability.
    /// </summary>
    public void AddOwed(decimal owed) => _liabilities += owed;

    /// <summary>
    /// Takes an account's balance back out of the total it was counted in.
    /// </summary>
    public void Remove(BalanceSnapshot balance) => Count(balance.Balance.Current, sign: -1);

    public NetWorthPoint ToPoint(DateOnly day) => new()
    {
        Date = day,
        Assets = _assets,
        Liabilities = _liabilities,
        NetWorth = _assets - _liabilities,
        IsDerived = _isDerived,
        IsStale = _isStale
    };

    private void Count(decimal current, int sign)
    {
        if (current >= 0)
        {
            _assets += sign * current;
        }
        else
        {
            _liabilities -= sign * current;
        }
    }
}
