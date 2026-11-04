using FinanceTracker.Domain.PayPeriods;

namespace FinanceTracker.Infrastructure.Reports;

/// <summary>
/// One month of a report, the user's period named after it, see <see cref="ReportPeriods"/>.
/// </summary>
/// <param name="Month">The month it's named after, 1 to 12.</param>
/// <param name="Period">Its days, from pay to pay or the calendar month.</param>
/// <param name="Days">The days with amounts, in order.</param>
internal sealed record ReportMonth(int Month, PayPeriod Period, IReadOnlyList<ReportAmount> Amounts,
    IReadOnlyList<ReportDay> Days);
