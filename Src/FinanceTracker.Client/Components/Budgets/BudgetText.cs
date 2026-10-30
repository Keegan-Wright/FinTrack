using System.Globalization;
using FinanceTracker.Client.Components.Common;
using FinanceTracker.Contracts.Budgets;

namespace FinanceTracker.Client.Components.Budgets;

/// <summary>
/// How budgets read on screen what's spent and left, the pace, and what carries over.
/// </summary>
public static class BudgetText
{
    /// <summary>
    /// e.g. <c>£120.00 of £400.00</c>.
    /// </summary>
    public static string SpentOf(BudgetStatus status) =>
        $"{CurrencyFormatter.Format(status.Spent)} of {CurrencyFormatter.Format(status.Available)}";

    /// <summary>
    /// e.g. <c>£280.00 left</c> or <c>£30.00 over</c>.
    /// </summary>
    public static string Remaining(BudgetStatus status) => status.Remaining < 0
        ? $"{CurrencyFormatter.Format(-status.Remaining)} over"
        : $"{CurrencyFormatter.Format(status.Remaining)} left";

    /// <summary>
    /// How spending compares with a steady pace, only meaningful in the current month.
    /// </summary>
    public static string Pace(BudgetStatus status)
    {
        decimal ahead = status.Spent - status.ExpectedByNow;
        if (ahead <= 0)
        {
            return "On pace";
        }

        return $"{CurrencyFormatter.Format(ahead)} ahead of pace";
    }

    /// <summary>
    /// What came in from the month before, null when nothing did.
    /// </summary>
    public static string? CarriedIn(BudgetStatus status)
    {
        if (status.CarriedIn > 0)
        {
            return $"Includes {CurrencyFormatter.Format(status.CarriedIn)} carried over";
        }

        if (status.CarriedIn < 0)
        {
            return $"{CurrencyFormatter.Format(-status.CarriedIn)} less for last month's overspend";
        }

        return null;
    }

    /// <summary>
    /// How much of what's spent is pending, null when none is.
    /// </summary>
    public static string? Pending(BudgetStatus status) => status.Pending > 0
        ? $"{CurrencyFormatter.Format(status.Pending)} still pending"
        : null;

    /// <summary>
    /// Why the budget can't track anything as it stands, null when it can.
    /// </summary>
    public static string? Problem(BudgetStatus status) => status.CoversNothing
        ? "Its tags were deleted, edit it to choose one so it tracks spending again"
        : null;

    /// <summary>
    /// For screen readers, e.g. <c>Groceries, £120.00 of £400.00 spent</c>.
    /// </summary>
    public static string BarLabel(BudgetStatus status) => $"{status.Name}, {SpentOf(status)} spent";

    /// <summary>
    /// How much of what's available is spent, 0 to 1. A budget with nothing available is full once anything's spent.
    /// </summary>
    public static double Progress(BudgetStatus status)
    {
        if (status.Available <= 0)
        {
            return status.Spent > 0 ? 1 : 0;
        }

        return Math.Clamp((double)(status.Spent / status.Available), 0, 1);
    }

    /// <summary>
    /// Where a steady pace would have reached by today, 0 to 1, for the marker on the bar.
    /// </summary>
    public static double PaceMarker(BudgetStatus status)
    {
        if (status.Available <= 0)
        {
            return 0;
        }

        return Math.Clamp((double)(status.ExpectedByNow / status.Available), 0, 1);
    }

    public static string RolloverName(RolloverMode mode) => mode switch
    {
        RolloverMode.CarryUnderspend => "Carry over what's left",
        RolloverMode.CarryBoth => "Carry over what's left and any overspend",
        _ => "Start each month afresh"
    };

    /// <summary>
    /// e.g. <c>March 2026</c>.
    /// </summary>
    public static string Month(DateOnly month) => month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
}
