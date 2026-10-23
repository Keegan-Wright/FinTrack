using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports.Account;

/// <summary>
/// Totals for one month.
/// </summary>
public class SpentInAccountReportMonthlyBreakdown : SharedReport
{
    /// <summary>
    /// The month's name in the server's culture, e.g. <c>January</c>.
    /// </summary>
    [Description("Month name, in the server's culture, for which the spending breakdown is provided")]
    public required string Month { get; init; }

    /// <summary>
    /// Each day of the month with transactions.
    /// </summary>
    [Description("Daily breakdown of spending for the specified month")]
    public IList<SpentInAccountReportDailyBreakdown> DailyBreakdown { get; init; } = [];
}
