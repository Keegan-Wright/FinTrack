using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports.Category;

/// <summary>
/// Spending for a tag, broken down by year, month and day. Totals are in the accounts' currency.
/// </summary>
public class SpentInCategoryReport : SharedReport
{
    /// <summary>
    /// The tag, a provider classification or a custom classification's name.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Each year with transactions, in the order first seen.
    /// </summary>
    [Description("Yearly breakdown of spending for the specified time period")]
    public IList<SpentInCategoryReportYearlyBreakdown> YearlyBreakdown { get; init; } = [];
}
