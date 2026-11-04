using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports.Account;

/// <summary>
/// Totals for one month, the user's period named after it, from pay to pay once their pay is picked up.
/// </summary>
public class SpentInAccountReportMonthlyBreakdown : SharedReport
{
    /// <summary>
    /// The month's name in the server's culture, e.g. <c>January</c>.
    /// </summary>
    [Description("Month name, in the server's culture, for which the spending breakdown is provided")]
    public required string Month { get; init; }

    /// <summary>
    /// The period's first day the pay day when the user's months run from pay to pay, otherwise the month's first
    /// day.
    /// </summary>
    [Description("The first day of the period the month covers, the pay day or the month's first day")]
    public DateOnly Start { get; init; }

    /// <summary>
    /// The period's last day,  the day before the next pay, otherwise the month's last day.
    /// </summary>
    [Description("The last day of the period the month covers, inclusive")]
    public DateOnly End { get; init; }

    /// <summary>
    /// Each day of the period with transactions.
    /// </summary>
    [Description("Daily breakdown of spending for the specified month")]
    public IList<SpentInAccountReportDailyBreakdown> DailyBreakdown { get; init; } = [];
}
