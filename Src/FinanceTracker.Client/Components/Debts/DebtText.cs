using System.Globalization;
using FinanceTracker.Client.Components.Common;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Client.Components.Debts;

/// <summary>
/// How debts read on screen what's owed, its terms, when it's paid off and what paying more would do.
/// </summary>
public static class DebtText
{
    /// <summary>
    /// e.g. <c>£900.00 left of £1,000.00</c>.
    /// </summary>
    public static string OwedOf(Debt debt) =>
        $"{CurrencyFormatter.Format(debt.Owed)} left of {CurrencyFormatter.Format(debt.StartingBalance)}";

    /// <summary>
    /// How much is paid off, 0 to 1.
    /// </summary>
    public static double Progress(Debt debt) => Math.Clamp((double)debt.PaidOffPercent / 100, 0, 1);

    public static bool IsPaidOff(Debt debt) => debt.Owed <= 0;

    /// <summary>
    /// e.g. <c>Car Finance Co · 6.9% APR · £250.00 a month</c>, with the account its balance comes from when linked.
    /// </summary>
    public static string Terms(Debt debt)
    {
        List<string> parts = [];
        if (!string.IsNullOrWhiteSpace(debt.Lender))
        {
            parts.Add(debt.Lender);
        }

        parts.Add(Rate(debt.InterestRate));
        parts.Add($"{CurrencyFormatter.Format(debt.MonthlyPayment)} a month");

        if (debt.AccountId is not null)
        {
            parts.Add($"Balance from {debt.AccountName ?? "a removed account"}");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// e.g. <c>6.9% APR</c>, or <c>No interest</c>.
    /// </summary>
    public static string Rate(decimal interestRate)
    {
        if (interestRate == 0)
        {
            return "No interest";
        }

        return $"{interestRate.ToString("0.##", CultureInfo.CurrentCulture)}% APR";
    }

    /// <summary>
    /// When it's paid off, e.g. <c>Paid off by Mar 2028, 18 more payments</c>.
    /// </summary>
    public static string Forecast(DebtForecast forecast)
    {
        if (forecast.PaidOffOn is not { } paidOffOn || forecast.Months is not { } months)
        {
            return "Never paid off at this payment, it doesn't cover the interest";
        }

        if (months == 0)
        {
            return "Paid off";
        }

        string payments = months == 1 ? "1 more payment" : $"{months} more payments";
        return $"Paid off by {Month(paidOffOn)}, {payments}";
    }

    /// <summary>
    /// e.g. <c>£123.45 interest still to pay</c>, null when there's none or it's never paid off.
    /// </summary>
    public static string? InterestToPay(DebtForecast forecast)
    {
        if (forecast.Interest is not { } interest || interest <= 0)
        {
            return null;
        }

        return $"{CurrencyFormatter.Format(interest)} interest still to pay";
    }

    /// <summary>
    /// How the forecast stands against the pay-off goal, null without a goal or once it's paid off.
    /// </summary>
    public static string? Goal(Debt debt)
    {
        if (debt.PayOffGoal is not { } goal || IsPaidOff(debt))
        {
            return null;
        }

        if (debt.OnTrack == true)
        {
            return $"On track for your goal of {Month(goal)}";
        }

        return $"Behind your goal of {Month(goal)}";
    }

    /// <summary>
    /// What paying <paramref name="monthlyPayment"/> a month does against its forecast now, e.g.
    /// <c>Paid off by Oct 2027, 7 months sooner, saving £120.00 interest</c>.
    /// </summary>
    public static string Compare(DebtForecast now, DebtForecast payingMore, decimal monthlyPayment)
    {
        string paying = $"At {CurrencyFormatter.Format(monthlyPayment)} a month";

        if (payingMore.PaidOffOn is not { } paidOffOn || payingMore.Months is not { } months)
        {
            return $"{paying} it's never paid off, that doesn't cover the interest";
        }

        string paidOff = $"{paying} it's paid off by {Month(paidOffOn)}";
        if (now.Months is not { } monthsNow)
        {
            return $"{paidOff}, when now it never is";
        }

        int sooner = monthsNow - months;
        if (sooner <= 0)
        {
            return $"{paidOff}, no sooner than now";
        }

        string soonerText = sooner == 1 ? "1 month sooner" : $"{sooner} months sooner";
        decimal saved = (now.Interest ?? 0) - (payingMore.Interest ?? 0);
        if (saved <= 0)
        {
            return $"{paidOff}, {soonerText}";
        }

        return $"{paidOff}, {soonerText}, saving {CurrencyFormatter.Format(saved)} interest";
    }

    /// <summary>
    /// e.g. <c>Car loan</c>, or <c>No debt</c> when the user said it pays none.
    /// </summary>
    public static string Label(TransactionDebt debt) => debt.DebtName ?? "No debt";

    /// <summary>
    /// Who said a transaction pays a debt, e.g. <c>Chosen by you</c> or <c>By your rule Car loan</c>.
    /// </summary>
    public static string Reason(TransferDecidedBy decidedBy, string? ruleName)
    {
        if (decidedBy == TransferDecidedBy.Rule)
        {
            return $"By your rule {ruleName}";
        }

        return "Chosen by you";
    }

    /// <summary>
    /// For screen readers, e.g. <c>Car loan, £900.00 left of £1,000.00</c>.
    /// </summary>
    public static string BarLabel(Debt debt) => $"{debt.Name}, {OwedOf(debt)}";

    public static string Month(DateOnly day) => day.ToString("MMM yyyy", CultureInfo.CurrentCulture);

    public static string Day(DateOnly day) => day.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
}
