using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Client.Components.Reports;

/// <summary>
/// One period of a report (all time, a year, a month or a day) with its totals and the periods within it. The three
/// report shapes the server returns all map onto this, see <see cref="ReportTree"/>.
/// </summary>
public sealed class ReportNode
{
    /// <summary>
    /// The short name, for a chart axis, e.g. <c>Aug</c>.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// The full name, for headings and lists, e.g. <c>August 2026</c>.
    /// </summary>
    public required string Title { get; init; }

    public int TotalTransactions { get; init; }

    /// <summary>
    /// Money in, 0 or more.
    /// </summary>
    public decimal TotalIn { get; init; }

    /// <summary>
    /// Money out, 0 or less.
    /// </summary>
    public decimal TotalOut { get; init; }

    public decimal Net => TotalIn + TotalOut;

    /// <summary>
    /// Days in the period.
    /// </summary>
    public int Days { get; init; }

    /// <summary>
    /// The periods within this one, in order, including empty ones.
    /// </summary>
    public IReadOnlyList<ReportNode> Children { get; init; } = [];

    /// <summary>
    /// What <see cref="Children"/> are, e.g. <c>month</c>, null for a day.
    /// </summary>
    public string? ChildPeriod { get; init; }

    /// <summary>
    /// The day, null for longer periods.
    /// </summary>
    public DateTime? Date { get; init; }

    /// <summary>
    /// The filters behind the report, narrowed to its account or tag. Lists a day's
    /// transactions, see <see cref="ReportTree.ForDay"/>.
    /// </summary>
    public FilterTransactions? Scope { get; init; }

    /// <summary>
    /// The account an account report is for.
    /// </summary>
    public Guid? AccountId { get; init; }

    /// <summary>
    /// Whether the totals count transfers between the user's accounts, set on the root only. Account reports do, an
    /// account's own money in and out, the rest leave them out so they aren't counted as income or spending.
    /// </summary>
    public bool IncludesTransfers { get; init; }

    public bool CanDrillInto => TotalTransactions > 0 && (Children.Count > 0 || Date is not null);
}
