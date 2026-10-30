using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.Bills;

/// <summary>
/// Works out how bills stand from the transactions that paid them, nothing about a period is stored.
/// </summary>
public static class BillCalculator
{
    /// <summary>
    /// Days past its due date before an unpaid period is late, for weekends, bank holidays and slow banks.
    /// </summary>
    public const int LateAfterDays = 3;

    /// <summary>
    /// Each period of <paramref name="bill"/> due from <paramref name="from"/> to <paramref name="to"/>, both included.
    /// </summary>
    /// <param name="transactions">The user's transactions, at least those paying the bill around those dates. Each
    /// counts for the due date nearest it, see <see cref="Bill.NearestDue"/>.</param>
    /// <param name="timeZone">Where the user is, so a payment falls on the day it was where they are.</param>
    /// <param name="today">Today where the user is, for when a period is late.</param>
    public static IReadOnlyList<BillPeriod> Periods(Bill bill, IEnumerable<BankTransaction> transactions,
        DateOnly from, DateOnly to, TimeZoneInfo timeZone, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(bill);
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(timeZone);

        Dictionary<DateOnly, List<BankTransaction>> byDue = transactions
            .Where(x => x.PaysBill(bill.Id))
            .GroupBy(x => bill.NearestDue(LocalDay(x.OccurredAt, timeZone)))
            .ToDictionary(static x => x.Key, static x => x.ToList());

        List<BillPeriod> periods = [];
        foreach (DateOnly due in bill.DueDatesBetween(from, to))
        {
            List<BankTransaction> payments = byDue.GetValueOrDefault(due, []);
            periods.Add(Period(bill, due, payments, timeZone, today));
        }

        return periods;
    }

    /// <summary>
    /// What <paramref name="bill"/> costs a month at the amount expected on <paramref name="on"/>, e.g. a weekly £10
    /// is £43.33, for the committed spending total. Nothing once it has ended by then.
    /// </summary>
    public static decimal MonthlyCost(Bill bill, DateOnly on)
    {
        ArgumentNullException.ThrowIfNull(bill);

        if (bill.EndedOn < on)
        {
            return 0;
        }

        return Math.Round(bill.ExpectedOn(on) * PerMonth(bill.Frequency), 2);
    }

    private static BillPeriod Period(Bill bill, DateOnly due, List<BankTransaction> payments, TimeZoneInfo timeZone,
        DateOnly today)
    {
        decimal expected = bill.ExpectedOn(due);

        if (payments.Count == 0)
        {
            BillState unpaid = BillState.Upcoming;
            if (today.DayNumber - due.DayNumber > LateAfterDays)
            {
                unpaid = BillState.Late;
            }

            return new BillPeriod(due, expected, unpaid, 0, null, []);
        }

        // Money out less refunds, so a payment refunded in full leaves nothing paid
        decimal paid = Math.Max(0, -payments.Sum(static x => x.Amount));
        DateOnly paidOn = payments.Max(x => LocalDay(x.OccurredAt, timeZone));

        BillState state = BillState.Changed;
        if (bill.IsExpected(paid, due))
        {
            state = BillState.Paid;
        }

        return new BillPeriod(due, expected, state, paid, paidOn, payments.Select(static x => x.Id).ToList());
    }

    private static decimal PerMonth(BillFrequency frequency) => frequency switch
    {
        BillFrequency.Weekly => 52m / 12,
        BillFrequency.Fortnightly => 26m / 12,
        BillFrequency.FourWeekly => 13m / 12,
        BillFrequency.Quarterly => 1m / 3,
        BillFrequency.Yearly => 1m / 12,
        _ => 1
    };

    /// <summary>
    /// The day <paramref name="utc"/> falls on where the user is.
    /// </summary>
    internal static DateOnly LocalDay(DateTime utc, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone));
}
