namespace FinanceTracker.Infrastructure.Bills;

/// <summary>
/// What <see cref="IBillSyncService.SyncAsync"/> did.
/// </summary>
/// <param name="Added">Bills seeded from new standing orders and direct debits.</param>
/// <param name="Ended">Bills ended because their standing order or direct debit went away or stopped.</param>
/// <param name="Matched">Transactions whose bill changed.</param>
/// <param name="PricesChanged">Bills whose expected amount followed a price change.</param>
public sealed record BillSyncSummary(int Added, int Ended, int Matched, int PricesChanged);
