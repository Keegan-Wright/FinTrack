using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.PayPeriods;

namespace FinanceTracker.Domain.Budgeting;

/// <summary>
/// Works out how budgets are doing from the user's transactions, nothing about spending is stored. A
/// <see cref="BudgetMonth"/> is the user's period named after that month, from pay to pay or a calendar month, see
/// <see cref="PayCalendar"/>, so past months are worked out the same way as the current one.
/// </summary>
public static class BudgetCalculator
{
    /// <summary>
    /// How <paramref name="budget"/> is doing in <paramref name="month"/>, rolling over from its first month.
    /// </summary>
    /// <param name="budget">The budget to work out.</param>
    /// <param name="transactions">The user's transactions, at least those from the budget's first month on.</param>
    /// <param name="month">The month to work out.</param>
    /// <param name="timeZone">Where the user is, so a transaction falls in the month it was where they are.</param>
    /// <param name="today">Today where the user is, for the pace.</param>
    /// <param name="calendar">The user's periods, which month each transaction counts in.</param>
    /// <returns>Null before the budget's first month.</returns>
    public static BudgetMonthResult? ForMonth(Budget budget, IEnumerable<BankTransaction> transactions,
        BudgetMonth month, TimeZoneInfo timeZone, DateOnly today, PayCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(timeZone);
        ArgumentNullException.ThrowIfNull(calendar);

        if (month < budget.FirstMonth)
        {
            return null;
        }

        Dictionary<BudgetMonth, List<BankTransaction>> byMonth = transactions
            .Where(budget.Covers)
            .GroupBy(x => MonthOf(x.OccurredAt.LocalDayIn(timeZone), calendar))
            .Where(x => x.Key >= budget.FirstMonth && x.Key <= month)
            .ToDictionary(static x => x.Key, static x => x.ToList());

        decimal carriedIn = 0;
        for (BudgetMonth current = budget.FirstMonth; current < month; current = current.Next)
        {
            decimal remaining = budget.LimitFor(current)!.Value + carriedIn - Spent(budget, byMonth, current);
            carriedIn = CarryOver(budget.RolloverMode, remaining);
        }

        decimal limit = budget.LimitFor(month)!.Value;
        List<BankTransaction> inMonth = byMonth.GetValueOrDefault(month, []);

        return new BudgetMonthResult(month, limit, carriedIn, Spent(budget, byMonth, month),
            -inMonth.Where(static x => x.IsPending).Sum(budget.AmountOf),
            ExpectedByNow(limit + carriedIn, month, today, calendar));
    }

    /// <summary>
    /// Money out in <paramref name="month"/> that none of <paramref name="budgets"/> cover, so nothing goes unseen,
    /// less refunds. A transaction is covered when a budget covers its account and has no tags, otherwise by the tags
    /// of the budgets that cover its account, only the parts carrying them when it's split. Untagged money in could be
    /// income or a refund, so only what the bank calls a purchase counts, see <see cref="BankTransaction.IsRefund"/>.
    /// Never below nothing.
    /// </summary>
    /// <param name="budgets">The user's budgets.</param>
    /// <param name="transactions">The user's transactions, at least those in the month.</param>
    /// <param name="month">The month to work out.</param>
    /// <param name="timeZone">Where the user is, so a transaction falls in the month it was where they are.</param>
    /// <param name="calendar">The user's periods, which month each transaction counts in.</param>
    public static decimal Unbudgeted(IEnumerable<Budget> budgets, IEnumerable<BankTransaction> transactions,
        BudgetMonth month, TimeZoneInfo timeZone, PayCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(timeZone);
        ArgumentNullException.ThrowIfNull(calendar);

        List<Budget> running = budgets
            .Where(x => month >= x.FirstMonth && !x.CoversNothing)
            .ToList();

        decimal net = transactions
            .Where(static x => x.CountsAsIncomeOrSpending && (x.Amount < 0 || x.IsRefund))
            .Where(x => MonthOf(x.OccurredAt.LocalDayIn(timeZone), calendar) == month)
            .Sum(x => x.Amount - Budgeted(running, x));

        return Math.Max(0, -net);
    }

    /// <summary>
    /// How much of <paramref name="transaction"/> any of <paramref name="budgets"/> cover: all of it when one covers
    /// its account without tags, otherwise what's under the tags of those covering its account.
    /// </summary>
    /// <param name="budgets">The budgets running in its month.</param>
    /// <param name="transaction">The transaction to check.</param>
    private static decimal Budgeted(List<Budget> budgets, BankTransaction transaction)
    {
        List<Budget> onItsAccount = budgets
            .Where(x => x.AccountIds.Count == 0 || x.AccountIds.Contains(transaction.AccountId))
            .ToList();

        if (onItsAccount.Exists(static x => x.TagIds.Count == 0))
        {
            return transaction.Amount;
        }

        HashSet<Guid> tags = onItsAccount.SelectMany(static x => x.TagIds).ToHashSet();
        return transaction.AmountUnder(tags);
    }

    /// <summary>
    /// Money out less refunds, so a purchase refunded in full leaves the budget as it was. What counts of each
    /// transaction is <see cref="Budget.AmountOf"/>. Never below nothing. Money in can cancel spending but not add to
    /// what the budget allows, e.g. income that happens to carry the tag.
    /// </summary>
    /// <param name="budget">The budget to work out.</param>
    /// <param name="byMonth">The user's transactions grouped by the month they count in.</param>
    /// <param name="month">The month to work out.</param>
    private static decimal Spent(Budget budget, Dictionary<BudgetMonth, List<BankTransaction>> byMonth,
        BudgetMonth month) =>
        Math.Max(0, -byMonth.GetValueOrDefault(month, []).Sum(budget.AmountOf));

    /// <summary>
    /// What of a month's <paramref name="remaining"/> carries into the next.
    /// </summary>
    /// <param name="mode">The budget's rollover mode.</param>
    /// <param name="remaining">What was left, negative when overspent.</param>
    private static decimal CarryOver(RolloverMode mode, decimal remaining) => mode switch
    {
        RolloverMode.CarryUnderspend => Math.Max(remaining, 0),
        RolloverMode.CarryBoth => remaining,
        _ => 0
    };

    /// <summary>
    /// The month the period <paramref name="day"/> falls in is named after.
    /// </summary>
    /// <param name="day">The day to look at.</param>
    /// <param name="calendar">The user's periods.</param>
    public static BudgetMonth MonthOf(DateOnly day, PayCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        PayPeriod period = calendar.Containing(day);
        return new BudgetMonth(period.Year, period.Month);
    }

    /// <summary>
    /// A straight line through the period, by day 15 of 30, half of <paramref name="available"/>.
    /// </summary>
    /// <param name="available">What the budget allows for the period.</param>
    /// <param name="month">The month to work out.</param>
    /// <param name="today">Today where the user is.</param>
    /// <param name="calendar">The user's periods.</param>
    private static decimal ExpectedByNow(decimal available, BudgetMonth month, DateOnly today, PayCalendar calendar)
    {
        BudgetMonth current = MonthOf(today, calendar);
        if (month < current)
        {
            return available;
        }

        if (month > current)
        {
            return 0;
        }

        PayPeriod period = calendar.Named(month.Year, month.Month);
        int daysIn = today.DayNumber - period.Start.DayNumber + 1;
        return Math.Round(available * daysIn / period.Days, 2);
    }
}
