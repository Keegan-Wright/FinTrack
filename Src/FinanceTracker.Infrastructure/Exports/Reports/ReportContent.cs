using FinanceTracker.Contracts.Reports;

namespace FinanceTracker.Infrastructure.Exports.Reports;

/// <summary>
/// Everything on a report PDF, gathered by <see cref="ReportDocumentBuilder"/> and laid out by
/// <see cref="ReportDocument"/>.
/// </summary>
public sealed class ReportContent
{
    public required ReportBreakdown Breakdown { get; init; }

    /// <summary>
    /// What it's about, most money out first all accounts together over time, or each account or tag.
    /// </summary>
    public required IReadOnlyList<ReportRow> Subjects { get; init; }

    /// <summary>
    /// The first day the filters cover, null from the start.
    /// </summary>
    public DateOnly? From { get; init; }

    /// <summary>
    /// The last day the filters cover, null up to today.
    /// </summary>
    public DateOnly? To { get; init; }

    /// <summary>
    /// The other filters chosen, by name, e.g. Accounts: Main, Savings. Empty for everything.
    /// </summary>
    public required IReadOnlyList<string> Filters { get; init; }

    /// <summary>
    /// The day it was made, where the user is.
    /// </summary>
    public required DateOnly MadeOn { get; init; }
}
