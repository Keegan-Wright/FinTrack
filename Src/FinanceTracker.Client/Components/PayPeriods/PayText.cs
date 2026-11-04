using System.Globalization;
using FinanceTracker.Client.Components.Common;
using FinanceTracker.Contracts.PayPeriods;

namespace FinanceTracker.Client.Components.PayPeriods;

/// <summary>
/// How the user's pay and pay periods read on screen.
/// </summary>
public static class PayText
{
    /// <summary>
    /// e.g. <c>the last working day of the month</c> or <c>the 15th of each month</c>.
    /// </summary>
    public static string When(Income income)
    {
        ArgumentNullException.ThrowIfNull(income);

        if (income.Kind == PayDayKind.LastWorkingDay)
        {
            return "the last working day of the month";
        }

        return $"the {Ordinal(income.Day)} of each month";
    }

    /// <summary>
    /// e.g. <c>ACME LTD · £2,400.00 into Main</c>.
    /// </summary>
    public static string Label(Income income)
    {
        ArgumentNullException.ThrowIfNull(income);
        return $"{income.Name} · {CurrencyFormatter.Format(income.Amount)} into {income.AccountName}";
    }

    /// <summary>
    /// e.g. <c>30 Oct – 29 Nov</c>, with the years when it spans two.
    /// </summary>
    public static string Period(DateOnly start, DateOnly end)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        if (start.Year == end.Year)
        {
            return $"{start.ToString("d MMM", culture)} – {end.ToString("d MMM", culture)}";
        }

        return $"{start.ToString("d MMM yyyy", culture)} – {end.ToString("d MMM yyyy", culture)}";
    }

    /// <summary>
    /// e.g. <c>30 Oct – 29 Nov · As it ended</c> for a month's section, the dates only while months run from pay to
    /// pay. Null when there's nothing to say.
    /// </summary>
    /// <param name="ended">Whether the month is over, so shown as it ended.</param>
    public static string? MonthCaption(YourPay? pay, DateOnly start, DateOnly end, bool ended)
    {
        List<string> parts = [];
        if (pay is { FollowsPay: true })
        {
            parts.Add(Period(start, end));
        }

        if (ended)
        {
            parts.Add("As it ended");
        }

        if (parts.Count == 0)
        {
            return null;
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// e.g. <c>1st</c>, <c>2nd</c>, <c>11th</c>, <c>23rd</c>.
    /// </summary>
    public static string Ordinal(int day)
    {
        int lastTwo = day % 100;
        if (lastTwo is >= 11 and <= 13)
        {
            return $"{day}th";
        }

        string suffix = (day % 10) switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th"
        };
        return $"{day}{suffix}";
    }
}
