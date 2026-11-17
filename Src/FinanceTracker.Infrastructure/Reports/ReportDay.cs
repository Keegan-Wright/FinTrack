namespace FinanceTracker.Infrastructure.Reports;

/// <summary>
/// One day of a report, see <see cref="ReportPeriods"/>.
/// </summary>
/// <param name="Date">The day where the user is.</param>
/// <param name="Amounts">Its amounts.</param>
internal sealed record ReportDay(DateOnly Date, IReadOnlyList<ReportAmount> Amounts);
