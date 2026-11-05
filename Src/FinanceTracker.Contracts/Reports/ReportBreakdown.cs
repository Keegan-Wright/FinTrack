namespace FinanceTracker.Contracts.Reports;

/// <summary>
/// How the reports page splits the transactions, each one a report from the server, and the PDF of it.
/// </summary>
public enum ReportBreakdown
{
    /// <summary>
    /// Everything together.
    /// </summary>
    OverTime,

    /// <summary>
    /// One report per account.
    /// </summary>
    ByAccount,

    /// <summary>
    /// One report per tag.
    /// </summary>
    ByTag
}
