using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

namespace FinanceTracker.Infrastructure.OpenBanking;

/// <summary>
/// Everything fetched for one account or card in a sync, see <see cref="IAccountSnapshotFetcher"/>. A null list means
/// that data wasn't synced, as it wasn't asked for or the fetch failed.
/// </summary>
/// <param name="Balances">The balances, null when not fetched or the fetch failed.</param>
/// <param name="StandingOrders">The standing orders, null when not fetched or the fetch failed.</param>
/// <param name="DirectDebits">The direct debits, null when not fetched or the fetch failed.</param>
/// <param name="Transactions">The settled transactions, null when not fetched or the fetch failed.</param>
/// <param name="PendingTransactions">The pending transactions, null when not fetched or the fetch failed.</param>
public sealed record AccountSnapshot(
    List<ExternalOpenBankingAccountBalance>? Balances,
    List<ExternalOpenBankingAccountStandingOrder>? StandingOrders,
    List<ExternalOpenBankingDirectDebit>? DirectDebits,
    List<ExternalOpenBankingAccountTransaction>? Transactions,
    List<ExternalOpenBankingAccountTransaction>? PendingTransactions);
