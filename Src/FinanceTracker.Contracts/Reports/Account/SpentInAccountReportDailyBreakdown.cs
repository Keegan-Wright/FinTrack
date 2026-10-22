using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports.Account;

/// <summary>
/// Totals for one day.
/// </summary>
public readonly struct SpentInAccountReportDailyBreakdown : IReport
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
    /// The day of the month, 1 to 31.
    /// </summary>
    public int Day { get; init; }
}
