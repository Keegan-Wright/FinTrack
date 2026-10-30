using System.Text.Json.Serialization;
using FinanceTracker.Contracts.Account;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Client.Components.Dashboard;

/// <summary>
/// Everything the dashboard shows for one date range. Persisted from the prerender as one value, with its range, so
/// the browser shows exactly what the server loaded.
/// </summary>
public sealed class DashboardData
{
    /// <summary>
    /// Nothing loaded yet.
    /// </summary>
    public static DashboardData Empty { get; } = new();

    public DashboardRangePreset Preset { get; init; } = DashboardRangePreset.ThisMonth;

    /// <summary>
    /// The range's start, local (server) time.
    /// </summary>
    public DateTime FromDate { get; init; }

    /// <summary>
    /// The range's end, local (server) time.
    /// </summary>
    public DateTime ToDate { get; init; }

    public SpentInTimePeriod? Spent { get; init; }

    /// <summary>
    /// Money in and out change against the previous range of the same length, null without a previous range.
    /// </summary>
    public decimal? TrendIn { get; init; }

    /// <inheritdoc cref="TrendIn"/>
    public decimal? TrendOut { get; init; }

    public decimal TotalBalance { get; init; }

    public decimal? HouseholdIncome { get; init; }

    /// <summary>
    /// The range's transactions, for the charts, capped at <see cref="ChartNote"/>'s limit.
    /// </summary>
    public List<Transaction> Transactions { get; init; } = [];

    /// <summary>
    /// Set when <see cref="Transactions"/> was capped.
    /// </summary>
    public string? ChartNote { get; init; }

    public List<UpcomingPayment> UpcomingPayments { get; init; } = [];

    public List<AccountAndTransactions> Accounts { get; init; } = [];

    public List<SavingsGoal> SavingsGoals { get; init; } = [];

    /// <summary>
    /// How this month's budgets are going, null until loaded.
    /// </summary>
    public BudgetMonthOverview? Budgets { get; init; }

    [JsonIgnore]
    public decimal Net => (Spent?.TotalIn ?? 0) + (Spent?.TotalOut ?? 0);
}
