using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.Debts;

/// <summary>
/// Works out what a <see cref="Debt"/> owes and when it's paid off.
/// </summary>
/// <remarks>
/// A debt not linked to an account owes its starting balance, less the payments linked to it, plus interest. Interest
/// is a twelfth of the yearly rate, added at the end of each month from <see cref="Debt.StartedOn"/> on what's owed
/// once that month's payments are taken off. Money out pays it off, and money in linked to it, such as a refund, adds
/// back. A debt linked to an account owes what the account's balance says.
/// </remarks>
public static class DebtCalculator
{
    /// <summary>
    /// The longest a forecast looks ahead, 50 years. A debt not paid off by then is never paid off.
    /// </summary>
    public const int MaximumMonths = 600;

    /// <param name="account">The debt's linked account, null when it isn't linked or the account has gone.</param>
    /// <param name="payments">The user's transactions, at least those paying off this debt.</param>
    /// <param name="timeZone">Where the user is, so a payment falls on the day it was where they are.</param>
    /// <param name="today">Today where the user is.</param>
    public static DebtProgress Progress(Debt debt, BankAccount? account, IEnumerable<BankTransaction> payments,
        TimeZoneInfo timeZone, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(debt);
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(timeZone);

        List<Paid> paid = PaymentsFor(debt, payments, timeZone, today);

        decimal owed = OwedByAccount(debt, account) ?? Walk(debt, paid, today).Last().Owed;
        DebtForecast forecast = Forecast(owed, debt.InterestRate, debt.MonthlyPayment, today);

        return new DebtProgress(owed, paid.Sum(static x => x.Amount), PaidOffPercent(debt, owed), forecast,
            OnTrack(debt, forecast));
    }

    /// <summary>
    /// What a debt not linked to an account owed on <paramref name="day"/>, for net worth on that day. Nothing before
    /// it started.
    /// </summary>
    /// <exception cref="InvalidOperationException">The debt is linked to an account, whose balance counts instead.
    /// </exception>
    public static decimal OwedOn(Debt debt, IEnumerable<BankTransaction> payments, TimeZoneInfo timeZone, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(debt);
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(timeZone);

        if (debt.AccountId is not null)
        {
            throw new InvalidOperationException($"Debt {debt.Id} owes what its account says.");
        }

        if (day < debt.StartedOn)
        {
            return 0;
        }

        return Walk(debt, PaymentsFor(debt, payments, timeZone, day), day).Last().Owed;
    }

    /// <summary>
    /// What a debt not linked to an account owed at its start, the end of each month since and today, oldest first.
    /// </summary>
    public static IReadOnlyList<DebtBalance> History(Debt debt, IEnumerable<BankTransaction> payments,
        TimeZoneInfo timeZone, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(debt);
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(timeZone);

        return Walk(debt, PaymentsFor(debt, payments, timeZone, today), today);
    }

    /// <summary>
    /// When <paramref name="owed"/> is paid off paying <paramref name="monthlyPayment"/> at the end of each month from
    /// <paramref name="today"/>, with interest added on what's left after each payment.
    /// </summary>
    /// <param name="interestRate">The yearly rate (APR) as a percentage.</param>
    public static DebtForecast Forecast(decimal owed, decimal interestRate, decimal monthlyPayment, DateOnly today)
    {
        if (owed <= 0)
        {
            return new DebtForecast(today, 0, 0);
        }

        if (monthlyPayment <= 0)
        {
            return DebtForecast.Never;
        }

        decimal left = owed;
        decimal interest = 0;

        for (int month = 1; month <= MaximumMonths; month++)
        {
            left -= monthlyPayment;
            if (left <= 0)
            {
                return new DebtForecast(today.AddMonths(month), month, interest);
            }

            decimal added = MonthlyInterest(left, interestRate);
            if (added >= monthlyPayment)
            {
                // It grows at least as fast as it's paid, so it never clears
                return DebtForecast.Never;
            }

            left += added;
            interest += added;
        }

        return DebtForecast.Never;
    }

    /// <summary>
    /// What a debt linked to an account owes, what's owed on the account as a positive amount and nothing when it's in
    /// credit. Null when the debt isn't linked, or the account is gone or has no balance yet.
    /// </summary>
    public static decimal? OwedByAccount(Debt debt, BankAccount? account)
    {
        ArgumentNullException.ThrowIfNull(debt);

        if (debt.AccountId is null || account?.Id != debt.AccountId || account.Balance is null)
        {
            return null;
        }

        return Math.Max(0, -account.Balance.Current);
    }

    /// <summary>
    /// What's owed at the start, at the end of each whole month since and on <paramref name="until"/>.
    /// </summary>
    private static List<DebtBalance> Walk(Debt debt, List<Paid> paid, DateOnly until)
    {
        decimal owed = debt.StartingBalance;
        List<DebtBalance> balances = [new(debt.StartedOn, owed)];

        DateOnly from = debt.StartedOn;
        for (int month = 1; month <= MaximumMonths; month++)
        {
            DateOnly monthEnd = debt.StartedOn.AddMonths(month);
            if (monthEnd > until)
            {
                break;
            }

            owed = Math.Max(0, owed - PaidBetween(paid, from, monthEnd));
            owed += MonthlyInterest(owed, debt.InterestRate);
            balances.Add(new DebtBalance(monthEnd, owed));
            from = monthEnd;
        }

        // The payments since the last month end, with no interest yet
        decimal sinceMonthEnd = PaidBetween(paid, from, until.AddDays(1));
        if (until > from || sinceMonthEnd != 0)
        {
            owed = Math.Max(0, owed - sinceMonthEnd);
            balances.Add(new DebtBalance(until, owed));
        }

        return balances;
    }

    /// <summary>
    /// Paid from <paramref name="from"/> up to but not including <paramref name="before"/>.
    /// </summary>
    private static decimal PaidBetween(List<Paid> paid, DateOnly from, DateOnly before) =>
        paid.Where(x => x.On >= from && x.On < before).Sum(static x => x.Amount);

    private static decimal MonthlyInterest(decimal owed, decimal interestRate) =>
        Math.Round(owed * interestRate / 100 / 12, 2, MidpointRounding.AwayFromZero);

    private static List<Paid> PaymentsFor(Debt debt, IEnumerable<BankTransaction> payments, TimeZoneInfo timeZone,
        DateOnly until)
    {
        List<Paid> paid = [];
        foreach (BankTransaction payment in payments)
        {
            if (!payment.PaysDebt(debt.Id))
            {
                continue;
            }

            DateOnly on = payment.OccurredAt.LocalDayIn(timeZone);
            if (on >= debt.StartedOn && on <= until)
            {
                paid.Add(new Paid(on, -payment.Amount));
            }
        }

        return paid;
    }

    private static decimal PaidOffPercent(Debt debt, decimal owed)
    {
        decimal percent = (debt.StartingBalance - owed) / debt.StartingBalance * 100;
        return Math.Clamp(percent, 0, 100);
    }

    private static bool? OnTrack(Debt debt, DebtForecast forecast)
    {
        if (debt.PayOffGoal is not { } goal)
        {
            return null;
        }

        return forecast.PaidOffOn is { } paidOffOn && paidOffOn <= goal;
    }


    private sealed record Paid(DateOnly On, decimal Amount);
}
