using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.Bills;

/// <summary>
/// Finds payments that repeat like a subscription, e.g. a streaming service on a card, which aren't standing orders
/// or direct debits so no bill was seeded for them. At least <see cref="RequiredPayments"/> payments to the same payee
/// from the same account, each one period after the last, within <see cref="SimilarPercent"/> of the latest amount,
/// and the latest recent enough that it's still being taken.
/// </summary>
/// <remarks>
/// Only suggests, see <see cref="Suggest"/>. Weekly to monthly, so a sync's recent transactions are enough to see three
/// in a row. Quarterly and yearly ones would need most of a year or years of history each time, the user adds those.
/// </remarks>
public static class SubscriptionDetector
{
    /// <summary>
    /// How many payments in a row before they're suggested.
    /// </summary>
    public const int RequiredPayments = 3;

    /// <summary>
    /// How far each payment can be from the latest's amount, as a percentage of it.
    /// </summary>
    public const decimal SimilarPercent = 10;

    /// <summary>
    /// A payee shorter than this in letters is too vague to tell one subscription from another.
    /// </summary>
    private const int MinimumKeyLength = 3;

    /// <summary>
    /// Tried in order, the first that fits wins, so payments every 28 days are four-weekly rather than monthly.
    /// </summary>
    private static readonly BillFrequency[] Frequencies =
    [
        BillFrequency.Weekly, BillFrequency.Fortnightly, BillFrequency.FourWeekly, BillFrequency.Monthly
    ];

    /// <summary>
    /// The subscriptions among <paramref name="transactions"/> no bill covers yet.
    /// </summary>
    /// <param name="bills">The user's bills. Payments to a running bill's payee aren't suggested again.</param>
    /// <param name="transactions">The user's transactions, a few periods' worth.</param>
    /// <param name="timeZone">Where the user is, so a payment falls on the day it was where they are.</param>
    /// <param name="today">Today where the user is, a subscription whose next payment is well overdue has stopped.
    /// </param>
    public static IReadOnlyList<DetectedSubscription> Detect(IReadOnlyCollection<Bill> bills,
        IEnumerable<BankTransaction> transactions, TimeZoneInfo timeZone, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(bills);
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(timeZone);

        IEnumerable<IGrouping<(Guid AccountId, string Payee), BankTransaction>> byPayee = transactions
            .Where(IsCandidate)
            .GroupBy(static x => (x.AccountId, Payee: PayeeNames.Key(PayeeNames.Name(x))))
            .Where(static x => x.Key.Payee.Length >= MinimumKeyLength);

        List<DetectedSubscription> detected = [];
        foreach (IGrouping<(Guid AccountId, string Payee), BankTransaction> payments in byPayee)
        {
            List<BankTransaction> ordered = payments.OrderBy(static x => x.OccurredAt).ToList();
            BankTransaction latest = ordered[^1];

            if (BillCovering(bills, latest) is not null)
            {
                continue;
            }

            DetectedSubscription? subscription = Repeating(ordered, timeZone, today);
            if (subscription is not null)
            {
                detected.Add(subscription);
            }
        }

        return detected;
    }

    /// <summary>
    /// Brings the user's suggestions in line with <paramref name="detected"/> new ones added, waiting ones refreshed,
    /// waiting ones no longer detected removed. Dismissed ones are left as they are so they're never suggested again.
    /// </summary>
    /// <param name="suggestions">The user's suggestions, those refreshed are changed in place.</param>
    public static SuggestionChanges Suggest(Guid userId, IReadOnlyCollection<BillSuggestion> suggestions,
        IReadOnlyCollection<DetectedSubscription> detected)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        ArgumentNullException.ThrowIfNull(detected);

        List<BillSuggestion> added = [];
        HashSet<BillSuggestion> stillDetected = [];

        foreach (DetectedSubscription subscription in detected)
        {
            BillSuggestion? existing = suggestions.FirstOrDefault(x => x.IsFor(subscription));
            if (existing is null)
            {
                added.Add(new BillSuggestion(userId, subscription));
                continue;
            }

            stillDetected.Add(existing);
            if (!existing.IsDismissed)
            {
                existing.Refresh(subscription);
            }
        }

        List<BillSuggestion> removed = suggestions
            .Where(x => !x.IsDismissed && !stillDetected.Contains(x))
            .ToList();

        return new SuggestionChanges(added, removed);
    }

    /// <summary>
    /// Settled money out that isn't a transfer and pays no bill. Pending payments are left out as the settled one
    /// replaces them, and a payment the user said pays no bill isn't suggested as one.
    /// </summary>
    private static bool IsCandidate(BankTransaction transaction) =>
        transaction.Amount < 0 && !transaction.IsPending && transaction.CountsAsIncomeOrSpending &&
        transaction.BillPayment is null;

    /// <summary>
    /// The running bill on <paramref name="payment"/>'s account it's paid to, null when there's none, so it isn't a
    /// new subscription.
    /// </summary>
    public static Bill? BillCovering(IReadOnlyCollection<Bill> bills, BankTransaction payment)
    {
        ArgumentNullException.ThrowIfNull(bills);
        ArgumentNullException.ThrowIfNull(payment);

        foreach (Bill bill in bills)
        {
            if (!bill.HasEnded && bill.AccountId == payment.AccountId && BillMatcher.IsPaidTo(bill, payment))
            {
                return bill;
            }
        }

        return null;
    }

    /// <summary>
    /// The longest run of payments ending with the latest that repeats at the first frequency to fit, null when none
    /// has <see cref="RequiredPayments"/> or the run has stopped.
    /// </summary>
    private static DetectedSubscription? Repeating(List<BankTransaction> ordered, TimeZoneInfo timeZone,
        DateOnly today)
    {
        BankTransaction latest = ordered[^1];
        DateOnly lastPaidOn = latest.OccurredAt.LocalDayIn(timeZone);

        foreach (BillFrequency frequency in Frequencies)
        {
            List<BankTransaction> run = RunEndingWithLatest(ordered, frequency, timeZone);
            if (run.Count < RequiredPayments)
            {
                continue;
            }

            if (Step(lastPaidOn, frequency).AddDays(BillMatcher.WindowDays) < today)
            {
                return null;
            }

            string name = PayeeNames.Name(latest);
            return new DetectedSubscription(latest.AccountId, PayeeNames.Key(name), name, frequency, -latest.Amount,
                run[0].OccurredAt.LocalDayIn(timeZone), lastPaidOn,
                run.Select(static x => x.Id).ToList());
        }

        return null;
    }

    /// <summary>
    /// Walks back from the latest payment while each earlier one is a period before the next, give or take
    /// <see cref="Slack"/>, and close to the latest's amount.
    /// </summary>
    /// <returns>Oldest first.</returns>
    private static List<BankTransaction> RunEndingWithLatest(List<BankTransaction> ordered, BillFrequency frequency,
        TimeZoneInfo timeZone)
    {
        decimal latestAmount = -ordered[^1].Amount;
        List<BankTransaction> run = [ordered[^1]];

        for (int index = ordered.Count - 2; index >= 0; index--)
        {
            BankTransaction earlier = ordered[index];

            DateOnly earlierDay = earlier.OccurredAt.LocalDayIn(timeZone);
            DateOnly laterDay = run[0].OccurredAt.LocalDayIn(timeZone);

            int offBy = Math.Abs(laterDay.DayNumber - Step(earlierDay, frequency).DayNumber);
            if (offBy > Slack(frequency) || !IsSimilar(-earlier.Amount, latestAmount))
            {
                break;
            }

            run.Insert(0, earlier);
        }

        return run;
    }

    private static bool IsSimilar(decimal amount, decimal latest) =>
        Math.Abs(amount - latest) <= latest * SimilarPercent / 100;

    /// <summary>
    /// One period on from <paramref name="day"/>.
    /// </summary>
    private static DateOnly Step(DateOnly day, BillFrequency frequency) => frequency switch
    {
        BillFrequency.Weekly => day.AddDays(7),
        BillFrequency.Fortnightly => day.AddDays(14),
        BillFrequency.FourWeekly => day.AddDays(28),
        _ => day.AddMonths(1)
    };

    /// <summary>
    /// How many days a payment can move from a period after the last, for weekends and bank holidays. Tighter for
    /// week based frequencies, so monthly payments don't pass as four-weekly.
    /// </summary>
    private static int Slack(BillFrequency frequency)
    {
        if (frequency == BillFrequency.Monthly)
        {
            return 4;
        }

        return 2;
    }
}
