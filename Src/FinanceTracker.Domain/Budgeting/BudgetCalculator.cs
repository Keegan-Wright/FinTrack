using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.Budgeting;

/// <summary>
/// Works out how budgets are doing from the user's transactions, nothing about spending is stored.
/// </summary>
public static class BudgetCalculator
{
    /// <summary>
    /// How <paramref name="budget"/> is doing in <paramref name="month"/>, rolling over from its first month.
    /// </summary>
    /// <param name="transactions">The user's transactions, at least those from the budget's first month on.</param>
    /// <param name="timeZone">Where the user is, so a transaction falls in the month it was where they are.</param>
    /// <param name="today">Today where the user is, for the pace.</param>
    /// <returns>Null before the budget's first month.</returns>
    public static BudgetMonthResult? ForMonth(Budget budget, IEnumerable<BankTransaction> transactions,
        BudgetMonth month, TimeZoneInfo timeZone, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(timeZone);

        if (month < budget.FirstMonth)
        {
            return null;
        }

        Dictionary<BudgetMonth, List<BankTransaction>> byMonth = transactions
            .Where(budget.Covers)
            .GroupBy(x => BudgetMonth.Of(x.OccurredAt, timeZone))
            .Where(x => x.Key >= budget.FirstMonth && x.Key <= month)
            .ToDictionary(static x => x.Key, static x => x.ToList());

        decimal carriedIn = 0;
        for (BudgetMonth current = budget.FirstMonth; current < month; current = current.Next)
        {
            decimal remaining = budget.LimitFor(current)!.Value + carriedIn - Spent(byMonth, current);
            carriedIn = CarryOver(budget.RolloverMode, remaining);
        }

        decimal limit = budget.LimitFor(month)!.Value;
        List<BankTransaction> inMonth = byMonth.GetValueOrDefault(month, []);

        return new BudgetMonthResult(month, limit, carriedIn, Spent(byMonth, month),
            -inMonth.Where(static x => x.IsPending).Sum(static x => x.Amount),
            ExpectedByNow(limit + carriedIn, month, today));
    }

    /// <summary>
    /// Money out in <paramref name="month"/> that none of <paramref name="budgets"/> cover, so nothing goes unseen,
    /// less refunds. Untagged money in could be income or a refund, so only what the bank calls a purchase counts as
    /// a refund, card refunds come back that way while salary and transfers don't. Never below nothing.
    /// </summary>
    public static decimal Unbudgeted(IEnumerable<Budget> budgets, IEnumerable<BankTransaction> transactions,
        BudgetMonth month, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(timeZone);

        List<Budget> running = budgets.Where(x => month >= x.FirstMonth).ToList();

        decimal net = transactions
            .Where(x => x.CountsAsIncomeOrSpending && (x.Amount < 0 || IsRefund(x)))
            .Where(x => BudgetMonth.Of(x.OccurredAt, timeZone) == month)
            .Where(x => !running.Exists(budget => budget.Covers(x)))
            .Sum(static x => x.Amount);

        return Math.Max(0, -net);
    }

    /// <summary>
    /// The bank's category for a card purchase, money in under it is a refund.
    /// </summary>
    private const string PurchaseCategory = "PURCHASE";

    private static bool IsRefund(BankTransaction transaction) =>
        transaction.Amount > 0 &&
        string.Equals(transaction.Category, PurchaseCategory, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Money out less refunds, so a purchase refunded in full leaves the budget as it was. Never below nothing: money
    /// in can cancel spending but not add to what the budget allows, e.g. income that happens to carry the tag.
    /// </summary>
    private static decimal Spent(Dictionary<BudgetMonth, List<BankTransaction>> byMonth, BudgetMonth month) =>
        Math.Max(0, -byMonth.GetValueOrDefault(month, []).Sum(static x => x.Amount));

    private static decimal CarryOver(RolloverMode mode, decimal remaining) => mode switch
    {
        RolloverMode.CarryUnderspend => Math.Max(remaining, 0),
        RolloverMode.CarryBoth => remaining,
        _ => 0
    };

    /// <summary>
    /// A straight line through the month: by the 15th of 30 days, half of <paramref name="available"/>.
    /// </summary>
    private static decimal ExpectedByNow(decimal available, BudgetMonth month, DateOnly today)
    {
        BudgetMonth current = BudgetMonth.Of(today);
        if (month < current)
        {
            return available;
        }

        if (month > current)
        {
            return 0;
        }

        return Math.Round(available * today.Day / month.Days, 2);
    }
}
