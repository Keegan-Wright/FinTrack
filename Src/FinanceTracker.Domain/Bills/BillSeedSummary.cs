namespace FinanceTracker.Domain.Bills;

/// <summary>
/// What <see cref="BillSeeder.Seed"/> did.
/// </summary>
/// <param name="Added">The bills it created, for the caller to save.</param>
/// <param name="Ended">How many bills it ended because their source went away or stopped.</param>
public sealed record BillSeedSummary(IReadOnlyList<Bill> Added, int Ended);
