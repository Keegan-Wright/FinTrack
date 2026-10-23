using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports.SpentInTimePeriod;

/// <summary>
/// Spending for all matching transactions, broken down by year, month and day. Totals are in the accounts' currency.
/// </summary>
public class SpentInTimePeriodReport : SharedReport
{
    /// <summary>
    /// Each year with transactions, in the order first seen.
    /// </summary>
    [Description("Yearly breakdown of spending for the specified time period")]
    public IList<SpentInTimePeriodReportYearlyBreakdown> YearlyBreakdown { get; init; } = [];
}
