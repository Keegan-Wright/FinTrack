namespace FinanceTracker.Infrastructure.Reports;

/// <summary>
/// One year of a report, its months named after the months in it, see <see cref="ReportPeriods"/>.
/// </summary>
/// <param name="Months">All twelve, empty ones too, so each has its dates.</param>
internal sealed record ReportYear(int Year, IReadOnlyList<ReportAmount> Amounts, IReadOnlyList<ReportMonth> Months);
