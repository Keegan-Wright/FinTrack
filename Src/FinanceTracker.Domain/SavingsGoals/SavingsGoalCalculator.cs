using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.SavingsGoals;

/// <summary>
/// Works out what a <see cref="SavingsGoal"/> has saved from its source, and whether it's on track for its target.
/// </summary>
public static class SavingsGoalCalculator
{
    /// <param name="accountBalance">The current balance of the goal's account, null when it has none or the account
    /// has gone.</param>
    /// <param name="transactions">The user's transactions, at least those from the goal's
    /// <see cref="SavingsGoal.TrackedFrom"/> on.</param>
    /// <param name="timeZone">Where the user is, so a transaction falls on the day it was where they are.</param>
    /// <param name="today">Today where the user is.</param>
    public static SavingsGoalProgress Progress(SavingsGoal goal, decimal? accountBalance,
        IEnumerable<BankTransaction> transactions, TimeZoneInfo timeZone, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(timeZone);

        decimal saved = Saved(goal, accountBalance, transactions, timeZone);
        return new SavingsGoalProgress(saved, ReachedBy(goal, saved, today), NeededPerMonth(goal, saved, today),
            OnTrack(goal, saved, today));
    }

    private static decimal Saved(SavingsGoal goal, decimal? accountBalance,
        IEnumerable<BankTransaction> transactions, TimeZoneInfo timeZone)
    {
        switch (goal.Source)
        {
            case SavingsGoalSource.Account:
                return accountBalance ?? 0;
            case SavingsGoalSource.Tags:
                return goal.Saved + Tagged(goal, transactions, timeZone);
            default:
                return goal.Saved;
        }
    }

    /// <summary>
    /// What a goal tracking tags had saved at the end of each day from the day it tracks from to
    /// <paramref name="today"/>: its starting amount plus what's counted, see <see cref="Progress"/>. Empty for any
    /// other goal, an account goal's history being its account's balances.
    /// </summary>
    /// <param name="transactions">The user's transactions, at least those from the goal's
    /// <see cref="SavingsGoal.TrackedFrom"/> on.</param>
    /// <param name="timeZone">Where the user is, so a transaction falls on the day it was where they are.</param>
    public static IReadOnlyList<SavedOn> History(SavingsGoal goal, IEnumerable<BankTransaction> transactions,
        TimeZoneInfo timeZone, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(timeZone);

        if (goal.Source != SavingsGoalSource.Tags || goal.TrackedFrom is not { } from)
        {
            return [];
        }

        Dictionary<DateOnly, decimal> savedOn = Counted(goal, transactions, timeZone)
            .GroupBy(x => LocalDay(x.OccurredAt, timeZone))
            .ToDictionary(static x => x.Key, x => x.Sum(t => t.AmountUnder(goal.TagIds)));

        List<SavedOn> history = [];
        decimal saved = goal.Saved;
        for (DateOnly day = from; day <= today; day = day.AddDays(1))
        {
            saved += savedOn.GetValueOrDefault(day);
            history.Add(new SavedOn(day, saved));
        }

        return history;
    }

    /// <summary>
    /// Tagged money in less tagged money out, of a split transaction only the parts with the goal's tags.
    /// </summary>
    private static decimal Tagged(SavingsGoal goal, IEnumerable<BankTransaction> transactions, TimeZoneInfo timeZone) =>
        Counted(goal, transactions, timeZone).Sum(x => x.AmountUnder(goal.TagIds));

    /// <summary>
    /// The transactions that count towards the goal. When both halves of a transfer are tagged, only the money in
    /// counts, so a rule tagging both sides doesn't cancel itself out.
    /// </summary>
    private static List<BankTransaction> Counted(SavingsGoal goal, IEnumerable<BankTransaction> transactions,
        TimeZoneInfo timeZone)
    {
        List<BankTransaction> tagged = transactions.Where(x => goal.Counts(x, timeZone)).ToList();
        HashSet<Guid> taggedIds = tagged.Select(static x => x.Id).ToHashSet();

        return tagged.Where(x => !IsOutgoingHalfOfTaggedTransfer(x, taggedIds)).ToList();
    }

    private static DateOnly LocalDay(DateTime utc, TimeZoneInfo timeZone) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone));

    private static bool IsOutgoingHalfOfTaggedTransfer(BankTransaction transaction, HashSet<Guid> taggedIds) =>
        transaction.Amount < 0 && transaction.Transfer?.PartnerTransactionId is { } partnerId &&
        taggedIds.Contains(partnerId);

    /// <summary>
    /// Saving the monthly contribution from next month on.
    /// </summary>
    private static DateOnly? ReachedBy(SavingsGoal goal, decimal saved, DateOnly today)
    {
        decimal toGo = goal.Target - saved;
        if (goal.Target <= 0 || goal.MonthlyContribution <= 0 || toGo <= 0)
        {
            return null;
        }

        int months = (int)Math.Ceiling(toGo / goal.MonthlyContribution);
        DateOnly firstOfMonth = new(today.Year, today.Month, 1);
        return firstOfMonth.AddMonths(months);
    }

    /// <summary>
    /// The months from next month to the target date's month, each taking an even share of what's left.
    /// </summary>
    private static decimal? NeededPerMonth(SavingsGoal goal, decimal saved, DateOnly today)
    {
        if (goal.Target <= 0 || goal.TargetDate is not { } targetDate)
        {
            return null;
        }

        int months = MonthsUntil(today, DateOnly.FromDateTime(targetDate));
        if (months <= 0)
        {
            return null;
        }

        decimal toGo = Math.Max(0, goal.Target - saved);
        return Math.Ceiling(toGo / months * 100) / 100;
    }

    private static bool? OnTrack(SavingsGoal goal, decimal saved, DateOnly today)
    {
        if (goal.Target <= 0)
        {
            return null;
        }

        if (saved >= goal.Target)
        {
            return true;
        }

        if (goal.TargetDate is not { } targetDate || goal.MonthlyContribution <= 0)
        {
            return null;
        }

        int months = MonthsUntil(today, DateOnly.FromDateTime(targetDate));
        return saved + goal.MonthlyContribution * Math.Max(months, 0) >= goal.Target;
    }

    private static int MonthsUntil(DateOnly today, DateOnly targetDate) =>
        (targetDate.Year - today.Year) * 12 + targetDate.Month - today.Month;
}
