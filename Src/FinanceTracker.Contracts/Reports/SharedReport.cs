using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports;

/// <inheritdoc cref="IReport"/>
public class SharedReport : IReport
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
}
