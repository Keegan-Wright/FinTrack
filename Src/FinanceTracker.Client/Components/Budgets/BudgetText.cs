using System.Globalization;
using FinanceTracker.Client.Components.Common;
using FinanceTracker.Contracts.Budgets;

namespace FinanceTracker.Client.Components.Budgets;

/// <summary>
/// How budgets read on screen what they cover, what's spent and left, the pace, and what carries over.
/// </summary>
public static class BudgetText
{
    /// <summary>
    /// e.g. <c>£120.00 of £400.00</c>.
    /// </summary>
    /// <param name="status">The budget's status.</param>
    public static string SpentOf(BudgetStatus status) =>
        $"{CurrencyFormatter.Format(status.Spent)} of {CurrencyFormatter.Format(status.Available)}";

    /// <summary>
    /// e.g. <c>£280.00 left</c> or <c>£30.00 over</c>.
    /// </summary>
    /// <param name="status">The budget's status.</param>
    public static string Remaining(BudgetStatus status) => status.Remaining < 0
        ? $"{CurrencyFormatter.Format(-status.Remaining)} over"
        : $"{CurrencyFormatter.Format(status.Remaining)} left";

    /// <summary>
    /// How spending compares with a steady pace, only meaningful in the current month.
    /// </summary>
    /// <param name="status">The budget's status.</param>
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
    /// <param name="status">The budget's status.</param>
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
    /// <param name="status">The budget's status.</param>
    public static string? Pending(BudgetStatus status) => status.Pending > 0
        ? $"{CurrencyFormatter.Format(status.Pending)} still pending"
        : null;

    /// <summary>
    /// Why the budget can't track anything as it stands, null when it can.
    /// </summary>
    /// <param name="status">The budget's status.</param>
    public static string? Problem(BudgetStatus status) => status.CoversNothing
        ? "Its tags were deleted, edit it to choose a tag or account so it tracks spending again"
        : null;

    /// <summary>
    /// What <paramref name="budget"/> covers, e.g. <c>Groceries, Household</c>, <c>All spending on Joint</c>,
    /// <c>All spending and money moved out of Joint</c> or <c>Groceries on Joint</c>. Null when none of its tags or
    /// accounts are left to name.
    /// </summary>
    /// <param name="budget">The budget.</param>
    /// <param name="ownTagNames">The user's tags by id, for their own budget. Someone else's names its own.</param>
    public static string? Covers(Budget budget, IReadOnlyDictionary<Guid, string> ownTagNames)
    {
        IReadOnlyDictionary<Guid, string> tagNames = ownTagNames;
        if (budget.SharedBy is not null)
        {
            tagNames = budget.TagNames;
        }

        string tags = Names(budget.TagIds, tagNames);
        string accounts = Names(budget.AccountIds, budget.AccountNames);

        if (accounts.Length == 0)
        {
            return tags.Length == 0 ? null : tags;
        }

        if (tags.Length == 0 && budget.CountsTransfersOut)
        {
            return $"All spending and money moved out of {accounts}";
        }

        if (tags.Length == 0)
        {
            return $"All spending on {accounts}";
        }

        return $"{tags} on {accounts}";
    }

    /// <summary>
    /// Says one of <paramref name="budget"/>'s accounts has gone, null when none has.
    /// </summary>
    /// <param name="budget">The budget.</param>
    public static string? AccountsProblem(Budget budget) =>
        budget.AccountIds.Exists(x => !budget.AccountNames.ContainsKey(x))
            ? "An account it covers was removed or isn't shared any more, edit it to choose another"
            : null;

    /// <summary>
    /// The names of <paramref name="ids"/> found in <paramref name="names"/>, in order, e.g. <c>Joint, Current</c>.
    /// </summary>
    /// <param name="ids">The ids.</param>
    /// <param name="names">The names by id.</param>
    private static string Names(IEnumerable<Guid> ids, IReadOnlyDictionary<Guid, string> names) =>
        string.Join(", ", ids.Where(names.ContainsKey).Select(x => names[x]));

    /// <summary>
    /// For screen readers, e.g. <c>Groceries, £120.00 of £400.00 spent</c>.
    /// </summary>
    /// <param name="status">The budget's status.</param>
    public static string BarLabel(BudgetStatus status) => $"{status.Name}, {SpentOf(status)} spent";

    /// <summary>
    /// How much of what's available is spent, 0 to 1. A budget with nothing available is full once anything's spent.
    /// </summary>
    /// <param name="status">The budget's status.</param>
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
    /// <param name="status">The budget's status.</param>
    public static double PaceMarker(BudgetStatus status)
    {
        if (status.Available <= 0)
        {
            return 0;
        }

        return Math.Clamp((double)(status.ExpectedByNow / status.Available), 0, 1);
    }

    /// <summary>
    /// What a rollover mode is called.
    /// </summary>
    /// <param name="mode">What happens to what's left at the end of a month.</param>
    public static string RolloverName(RolloverMode mode) => mode switch
    {
        RolloverMode.CarryUnderspend => "Carry over what's left",
        RolloverMode.CarryBoth => "Carry over what's left and any overspend",
        _ => "Start each month afresh"
    };

    /// <summary>
    /// e.g. <c>March 2026</c>.
    /// </summary>
    /// <param name="month">The month's first day.</param>
    public static string Month(DateOnly month) => month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);

    /// <summary>
    /// How a household budget is shared, e.g. <c>Household · Jane Doe's</c>, or <c>Household</c> for the user's own.
    /// Null for one that isn't shared.
    /// </summary>
    /// <param name="status">The budget's status.</param>
    public static string? SharedText(BudgetStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (status.SharedBy is { } sharedBy)
        {
            return $"Household · {sharedBy}'s";
        }

        if (status.IsHousehold)
        {
            return "Household";
        }

        return null;
    }
}
