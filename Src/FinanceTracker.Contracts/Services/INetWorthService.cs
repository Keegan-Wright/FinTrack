using FinanceTracker.Contracts.NetWorth;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's net worth and account balances over time, from the snapshots each sync takes and the history
/// worked out before them.
/// </summary>
public interface INetWorthService
{
    /// <summary>
    /// Net worth from <paramref name="from"/> on, how it's changed this month and year, and what makes it up today.
    /// </summary>
    /// <param name="from">The first day, null for all of it.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    Task<NetWorthHistory> GetNetWorthAsync(DateOnly? from, CancellationToken cancellationToken);

    /// <summary>
    /// Each of the user's accounts' closing balances from <paramref name="from"/> on.
    /// </summary>
    /// <param name="from">The first day, null for all of it.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    IAsyncEnumerable<AccountBalanceHistory> GetBalanceHistoriesAsync(DateOnly? from,
        CancellationToken cancellationToken);
}
