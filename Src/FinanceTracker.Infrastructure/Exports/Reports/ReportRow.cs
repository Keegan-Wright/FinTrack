namespace FinanceTracker.Infrastructure.Exports.Reports;

/// <summary>
/// One line of a report PDF with its totals. What it's about (all accounts, an account or a tag), a year or a month,
/// with the periods within it.
/// </summary>
public sealed class ReportRow
{
    /// <summary>
    /// e.g. Groceries, 2026 or November 2026.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The short name, for a chart, e.g. Nov.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// The days a month covers when it runs from pay day, e.g. 30 Oct to 29 Nov 2026, null otherwise.
    /// </summary>
    public string? Dates { get; init; }

    public int TotalTransactions { get; init; }

    /// <summary>
    /// Money in, 0 or more.
    /// </summary>
    public decimal TotalIn { get; init; }

    /// <summary>
    /// Money out, 0 or less.
    /// </summary>
    public decimal TotalOut { get; init; }

    /// <summary>
    /// The years in what it's about, or the twelve months in a year, oldest first, empty for a month.
    /// </summary>
    public IReadOnlyList<ReportRow> Periods { get; init; } = [];
}
