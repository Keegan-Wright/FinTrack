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
    Task<ReportContent> BuildAsync(ExportReport report, CancellationToken cancellationToken);
}
