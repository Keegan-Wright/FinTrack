using FinanceTracker.Domain.OpenBanking.Accounts;

namespace FinanceTracker.Domain.Bills;

/// <summary>
/// Keeps the bills seeded from standing orders and direct debits in step with what the bank last reported. A sync
/// replaces those, so each bill follows its source by <see cref="Bill.SourceId"/> and ends when the source
/// is gone.
/// </summary>
/// <remarks>
/// Only the schedule, the price the bank reports and whether it runs are followed. The name and tolerance are the
/// user's to change.
/// </remarks>
public static class BillSeeder
{
    /// <summary>
    /// How a bank marks a standing order or direct debit that no longer runs.
    /// </summary>
    private const string InactiveStatus = "Inactive";

    /// <summary>
    /// Creates a bill for each running standing order and direct debit without one, refreshes those with one, and ends
    /// any whose source has gone.
    /// </summary>
    /// <param name="accounts">The user's accounts with their standing orders and direct debits.</param>
    /// <param name="bills">The user's bills, those changed are changed in place.</param>
    /// <param name="today">Today where the user is, the day a bill whose source went away ends.</param>
    public static BillSeedSummary Seed(IEnumerable<BankAccount> accounts, IReadOnlyCollection<Bill> bills,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(bills);

        Dictionary<Guid, Bill> bySource = bills
            .Where(static x => x.SourceId is not null)
            .ToDictionary(static x => x.SourceId!.Value);

        List<Bill> added = [];
        HashSet<Guid> sources = [];

        foreach (BankAccount account in accounts)
        {
            foreach (StandingOrder standingOrder in account.StandingOrders)
            {
                sources.Add(standingOrder.Id);
                Bill? bill = bySource.GetValueOrDefault(standingOrder.Id);
                if (bill is null)
                {
                    AddIfRunning(added, FromStandingOrder(account, standingOrder));
                }
                else
                {
                    Refresh(bill, standingOrder, today);
                }
            }

            foreach (DirectDebit directDebit in account.DirectDebits)
            {
                sources.Add(directDebit.Id);
                Bill? bill = bySource.GetValueOrDefault(directDebit.Id);
                if (bill is null)
                {
                    AddIfRunning(added, FromDirectDebit(account, directDebit));
                }
                else
                {
                    Refresh(bill, directDebit, today);
                }
            }
        }

        int ended = 0;
        foreach (Bill bill in bySource.Values)
        {
            if (!sources.Contains(bill.SourceId!.Value) && bill.End(today))
            {
                ended++;
            }
        }

        return new BillSeedSummary(added, ended);
    }

    /// <summary>
    /// A bill for <paramref name="standingOrder"/>, due from its first payment on its schedule. Null when it has no
    /// next payment or amount to go on, or it has stopped.
    /// </summary>
    public static Bill? FromStandingOrder(BankAccount account, StandingOrder standingOrder)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(standingOrder);

        DateOnly? next = Known(standingOrder.NextPaymentDate);
        decimal amount = Math.Abs(standingOrder.NextPaymentAmount);

        if (next is null || amount == 0 || IsInactive(standingOrder.Status))
        {
            return null;
        }

        DateOnly startsOn = Known(standingOrder.FirstPaymentDate) ?? next.Value;
        if (startsOn > next)
        {
            startsOn = next.Value;
        }

        BillFrequency frequency = StandingOrderFrequency.Parse(standingOrder.Frequency) ?? BillFrequency.Monthly;

        Bill bill = new(account.UserId, account.Id, standingOrder.Payee, standingOrder.Payee, frequency, amount,
            startsOn, BillSource.StandingOrder, standingOrder.Id);

        bill.Reschedule(frequency, next.Value);
        EndOnFinalPayment(bill, standingOrder);

        return bill;
    }

    /// <summary>
    /// A bill for <paramref name="directDebit"/>, monthly from its previous payment as banks don't say how often
    /// they're taken. Null before it's been paid, or once it has stopped.
    /// </summary>
    public static Bill? FromDirectDebit(BankAccount account, DirectDebit directDebit)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(directDebit);

        DateOnly? previous = Known(directDebit.PreviousPaymentDate);
        decimal amount = Math.Abs(directDebit.PreviousPaymentAmount);

        if (previous is null || amount == 0 || IsInactive(directDebit.Status))
        {
            return null;
        }

        return new Bill(account.UserId, account.Id, directDebit.Name, directDebit.Name, BillFrequency.Monthly, amount,
            previous.Value, BillSource.DirectDebit, directDebit.Id);
    }

    private static void AddIfRunning(List<Bill> added, Bill? bill)
    {
        if (bill is not null)
        {
            added.Add(bill);
        }
    }

    /// <summary>
    /// Follows the standing order's schedule and the price of its next payment, from that payment on.
    /// </summary>
    private static void Refresh(Bill bill, StandingOrder standingOrder, DateOnly today)
    {
        DateOnly? next = Known(standingOrder.NextPaymentDate);
        if (next is not null)
        {
            BillFrequency frequency = StandingOrderFrequency.Parse(standingOrder.Frequency) ?? bill.Frequency;
            bill.Reschedule(frequency, next.Value);

            decimal amount = Math.Abs(standingOrder.NextPaymentAmount);
            if (amount > 0)
            {
                bill.ChangeAmount(amount, next.Value);
            }
        }

        if (IsInactive(standingOrder.Status))
        {
            bill.End(today);
            return;
        }

        if (!EndOnFinalPayment(bill, standingOrder))
        {
            bill.Reopen();
        }
    }

    /// <summary>
    /// Ends it once the bank says it has stopped and runs it again if it restarts. Its schedule and price are the
    /// user's, the bank doesn't send them.
    /// </summary>
    private static void Refresh(Bill bill, DirectDebit directDebit, DateOnly today)
    {
        if (IsInactive(directDebit.Status))
        {
            bill.End(today);
            return;
        }

        bill.Reopen();
    }

    /// <returns>Whether the standing order has a final payment, so the bill ends then.</returns>
    private static bool EndOnFinalPayment(Bill bill, StandingOrder standingOrder)
    {
        DateOnly? final = Known(standingOrder.FinalPaymentDate);
        if (final is null)
        {
            return false;
        }

        if (bill.EndedOn != final)
        {
            bill.Reopen();
            bill.End(final.Value);
        }

        return true;
    }

    private static bool IsInactive(string? status) =>
        string.Equals(status, InactiveStatus, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The day, null when the bank left it out, which arrives as the earliest date there is.
    /// </summary>
    private static DateOnly? Known(DateTime date)
    {
        if (date.Year <= 1900)
        {
            return null;
        }

        return DateOnly.FromDateTime(date);
    }
}
