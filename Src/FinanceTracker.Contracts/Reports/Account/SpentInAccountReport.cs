using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports.Account;

/// <summary>
/// Spending for an account, broken down by year, month and day. Totals are in the accounts' currency.
/// </summary>
public class SpentInAccountReport : SharedReport
{
    /// <summary>
    /// Each year with transactions, in the order first seen.
    /// </summary>
    [Description("Yearly breakdown of spending for the specified time period")]
    public IList<SpentInAccountReportYearlyBreakdown> YearlyBreakdown { get; init; } = [];

    /// <summary>
    /// The account's name.
    /// </summary>
    public required string AccountName { get; init; }
}
