using System.ComponentModel;
using FinanceTracker.Contracts.Reports;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Contracts.Exports;

/// <summary>
/// Which report to make a PDF of, as the reports page shows it.
/// </summary>
public class ExportReport
{
    /// <summary>
    /// Over time, by account or by tag.
    /// </summary>
    [Description("Over time, by account or by tag")]
    public ReportBreakdown Breakdown { get; init; }

    /// <summary>
    /// The transactions it covers, as the reports page filters them.
    /// </summary>
    [Description("The transactions it covers")]
    public FilterTransactions Filters { get; init; } = new();
}
