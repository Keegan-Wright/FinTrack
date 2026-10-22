namespace FinanceTracker.Contracts.Reports;

/// <summary>
/// Totals every report and breakdown carries.
/// </summary>
public interface IReport
{
    /// <summary>
    /// How many transactions are counted.
    /// </summary>
    int TotalTransactions { get; }

    /// <summary>
    /// Money in, 0 or more.
    /// </summary>
    decimal TotalIn { get; }

    /// <summary>
    /// Money out, 0 or less.
    /// </summary>
    decimal TotalOut { get; }
}
