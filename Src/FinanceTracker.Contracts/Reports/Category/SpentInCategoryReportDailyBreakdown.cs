using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports.Category;

/// <summary>
/// Totals for one day.
/// </summary>
public readonly struct SpentInCategoryReportDailyBreakdown : IReport
{
    /// <inheritdoc/>
    [Description("Total number of transactions")]
    public int TotalTransactions { get; init; }

    /// <inheritdoc/>
    [Description("Total amount incoming")]
    public decimal TotalIn { get; init; }

    /// <inheritdoc/>
    [Description("Total amount outgoing")]
    public decimal TotalOut { get; init; }

    /// <summary>
    /// The day of the month.
    /// </summary>
    public int Day { get; init; }
}
