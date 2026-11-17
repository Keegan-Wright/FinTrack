using FinanceTracker.Contracts.Exports;

namespace FinanceTracker.Infrastructure.Exports.Reports;

/// <summary>
/// Gathers a <see cref="ReportContent"/> through the report service the reports page reads.
/// </summary>
public interface IReportDocumentBuilder
{
    /// <summary>
    /// What <paramref name="report"/> shows, for its file.
    /// </summary>
    /// <param name="report">What the report is of, and its filters.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    Task<ReportContent> BuildAsync(ExportReport report, CancellationToken cancellationToken);
}
