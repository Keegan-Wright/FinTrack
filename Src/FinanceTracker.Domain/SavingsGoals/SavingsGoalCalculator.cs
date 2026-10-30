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
    /// Tagged money in less tagged money out. When both halves of a transfer are tagged, only the money in counts, so a
    /// rule tagging both sides doesn't cancel itself out.
    /// </summary>
    private static decimal Tagged(SavingsGoal goal, IEnumerable<BankTransaction> transactions, TimeZoneInfo timeZone)
    {
        DateOnly from = goal.TrackedFrom!.Value;

        List<BankTransaction> tagged = transactions
            .Where(x => x.CustomClassificationIds.Any(goal.TagIds.Contains))
            .Where(x => LocalDay(x.OccurredAt, timeZone) >= from)
            .ToList();

        HashSet<Guid> taggedIds = tagged.Select(static x => x.Id).ToHashSet();

        return tagged
            .Where(x => !IsOutgoingHalfOfTaggedTransfer(x, taggedIds))
            .Sum(static x => x.Amount);
    }

    private static bool IsOutgoingHalfOfTaggedTransfer(BankTransaction transaction, HashSet<Guid> taggedIds) =>
        transaction.Amount < 0 && transaction.Transfer?.PartnerTransactionId is { } partnerId &&
        taggedIds.Contains(partnerId);

    private static DateOnly LocalDay(DateTime utc, TimeZoneInfo timeZone) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone));

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
