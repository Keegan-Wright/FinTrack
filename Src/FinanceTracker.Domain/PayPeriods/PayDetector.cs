using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.PayPeriods;

/// <summary>
/// Finds money that comes in like pay at least <see cref="RequiredPays"/> payments from the same payer into the same
/// account in the last <see cref="LookBackMonths"/> months, about a month apart and within <see cref="SimilarPercent"/>
/// of the latest's amount.
/// </summary>
public static class PayDetector
{
    public const int RequiredPays = 3;

    /// <summary>
    /// Three months, so pay periods work soon after a bank is connected.
    /// </summary>
    public const int LookBackMonths = 3;

    /// <summary>
    /// Pay moves with overtime, bonuses and tax, so more than a subscription's.
    /// </summary>
    public const decimal SimilarPercent = 25;

    /// <summary>
    /// How many days a pay can be from a month after the last, as pay due on a weekend or bank holiday comes in
    /// early. Also how far before the look back an early pay still counts.
    /// </summary>
    public const int SlackDays = 4;

    /// <summary>
    /// A payer shorter than this in letters is too vague to tell one from another.
    /// </summary>
    private const int MinimumKeyLength = 3;

    /// <summary>
    /// Every income in <paramref name="transactions"/> that comes in like pay, the largest first, the one taken as the
    /// pay unless the user chooses another.
    /// </summary>
    /// <param name="transactions">The user's transactions, at least the last <see cref="LookBackMonths"/> months.
    /// </param>
    /// <param name="timeZone">Where the user is, so a pay falls on the day it was where they are.</param>
    /// <param name="today">Today where the user is.</param>
    public static IReadOnlyList<DetectedPay> Detect(IEnumerable<BankTransaction> transactions, TimeZoneInfo timeZone,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(timeZone);

        DateOnly from = today.AddMonths(-LookBackMonths).AddDays(-SlackDays);
        IEnumerable<IGrouping<(Guid AccountId, string Payer), BankTransaction>> byPayer = transactions
            .Where(IsCandidate)
            .Where(x => x.OccurredAt.LocalDayIn(timeZone) >= from)
            .GroupBy(static x => (x.AccountId, Payer: PayeeNames.Key(PayeeNames.Name(x))))
            .Where(static x => x.Key.Payer.Length >= MinimumKeyLength);

        List<DetectedPay> detected = [];
        foreach (IGrouping<(Guid AccountId, string Payer), BankTransaction> payments in byPayer)
        {
            List<BankTransaction>? run = LatestMonthlyRun(payments.OrderBy(static x => x.OccurredAt).ToList(),
                timeZone);
            if (run is null)
            {
                continue;
            }

            BankTransaction latest = run[^1];
            List<DateOnly> payDays = run.Select(x => x.OccurredAt.LocalDayIn(timeZone)).ToList();
            PayPattern pattern = PayPattern.Fitting(payDays) ?? PayPattern.OnDay(payDays[^1].Day);
            detected.Add(new DetectedPay(latest.AccountId, payments.Key.Payer, PayeeNames.Name(latest),
                latest.Amount, pattern, payDays));
        }

        return detected.OrderByDescending(static x => x.Amount).ToList();
    }

    /// <summary>
    /// Settled money in that isn't a transfer between the user's accounts.
    /// </summary>
    private static bool IsCandidate(BankTransaction transaction) =>
        transaction.Amount > 0 && !transaction.IsPending && transaction.CountsAsIncomeOrSpending;

    /// <summary>
    /// The run of at least <see cref="RequiredPays"/> ending with the latest payment it can, so a refund or expenses
    /// from the payer after the latest pay don't hide it. Null when there's none.
    /// </summary>
    private static List<BankTransaction>? LatestMonthlyRun(List<BankTransaction> ordered, TimeZoneInfo timeZone)
    {
        for (int index = ordered.Count - 1; index >= RequiredPays - 1; index--)
        {
            List<BankTransaction> run = MonthlyRun(ordered, ordered[index], timeZone);
            if (run.Count >= RequiredPays)
            {
                return run;
            }
        }

        return null;
    }

    /// <summary>
    /// Walks back from <paramref name="latest"/> to the payment closest to a month before each, give or take
    /// <see cref="SlackDays"/> and close to the latest's amount, so a refund or expenses from the same payer in
    /// between are passed over.
    /// </summary>
    /// <returns>Oldest first.</returns>
    private static List<BankTransaction> MonthlyRun(List<BankTransaction> ordered, BankTransaction latest,
        TimeZoneInfo timeZone)
    {
        List<BankTransaction> run = [latest];

        while (ClosestToAMonthBefore(ordered, run[0], latest.Amount, timeZone) is { } earlier)
        {
            run.Insert(0, earlier);
        }

        return run;
    }

    /// <summary>
    /// The payment before <paramref name="later"/> closest to a month before it, null when none is within
    /// <see cref="SlackDays"/> with an amount like <paramref name="latestAmount"/>.
    /// </summary>
    private static BankTransaction? ClosestToAMonthBefore(List<BankTransaction> ordered, BankTransaction later,
        decimal latestAmount, TimeZoneInfo timeZone)
    {
        DateOnly monthBefore = later.OccurredAt.LocalDayIn(timeZone).AddMonths(-1);
        BankTransaction? closest = null;
        int closestOffBy = SlackDays + 1;

        foreach (BankTransaction payment in ordered)
        {
            if (payment.OccurredAt >= later.OccurredAt || !IsSimilar(payment.Amount, latestAmount))
            {
                continue;
            }

            int offBy = Math.Abs(payment.OccurredAt.LocalDayIn(timeZone).DayNumber - monthBefore.DayNumber);
            if (offBy < closestOffBy)
            {
                closest = payment;
                closestOffBy = offBy;
            }
        }

        return closest;
    }

    private static bool IsSimilar(decimal amount, decimal latest) =>
        Math.Abs(amount - latest) <= latest * SimilarPercent / 100;
}
