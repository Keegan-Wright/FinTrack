namespace FinanceTracker.Infrastructure.Reports;

/// <summary>
/// What a transaction adds to a report, all of it, or of a split transaction only the parts the report is about.
/// </summary>
/// <param name="AccountId">The account it's on.</param>
/// <param name="OccurredAt">When it happened.</param>
/// <param name="Amount">Signed: negative is money out.</param>
internal sealed record ReportAmount(Guid AccountId, DateTime OccurredAt, decimal Amount);
