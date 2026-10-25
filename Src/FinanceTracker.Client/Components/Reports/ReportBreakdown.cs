namespace FinanceTracker.Client.Components.Reports;

/// <summary>
/// How the reports page splits the transactions, each one a report from the server.
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
