using System.Globalization;
using FinanceTracker.Client.Components.Common;
using FinanceTracker.Contracts.NetWorth;
using MudBlazor;

namespace FinanceTracker.Client.Components.NetWorth;

/// <summary>
/// How balances and net worth over time read and chart.
/// </summary>
public static class NetWorthText
{
    /// <summary>
    /// <paramref name="points"/> split into the days synced from the bank and the days worked out from transactions.
    /// Where one meets the other the day is in both, so the lines join up.
    /// </summary>
    public static IReadOnlyList<HistoryChartPoint> ChartPoints(IReadOnlyList<BalancePoint> points)
    {
        List<HistoryChartPoint> chart = [];
        for (int index = 0; index < points.Count; index++)
        {
            BalancePoint point = points[index];
            bool joinsDerived = !point.IsDerived && index > 0 && points[index - 1].IsDerived;

            decimal? synced = null;
            decimal? derived = null;
            if (point.IsDerived)
            {
                derived = point.Amount;
            }
            else
            {
                synced = point.Amount;
            }

            if (joinsDerived)
            {
                derived = point.Amount;
            }

            chart.Add(new HistoryChartPoint(Day(point.Date), synced, derived));
        }

        return chart;
    }

    /// <summary>
    /// Net worth on each day as a chartable balance.
    /// </summary>
    public static IReadOnlyList<BalancePoint> Points(NetWorthHistory history) => history.Points
        .Select(static x => new BalancePoint { Date = x.Date, Amount = x.NetWorth, IsDerived = x.IsDerived })
        .ToList();

    /// <summary>
    /// How net worth moved across <paramref name="points"/>, e.g. <c>Up £120.00 since 1 Nov</c>, null with fewer
    /// than two days to compare.
    /// </summary>
    public static string? ChangeAcross(IReadOnlyList<NetWorthPoint> points)
    {
        if (points.Count < 2)
        {
            return null;
        }

        decimal change = points[^1].NetWorth - points[0].NetWorth;
        string since = points[0].Date.ToString("d MMM", CultureInfo.CurrentCulture);
        if (change == 0)
        {
            return $"No change since {since}";
        }

        string direction = change > 0 ? "Up" : "Down";
        return $"{direction} {CurrencyFormatter.Format(Math.Abs(change))} since {since}";
    }

    /// <summary>
    /// e.g. <c>Up £120.00 this month</c>, or <c>No history from before this month</c>.
    /// </summary>
    public static string Change(decimal? change, string period)
    {
        if (change is not { } amount)
        {
            return $"No history from before this {period}";
        }

        if (amount == 0)
        {
            return $"No change this {period}";
        }

        string direction = amount > 0 ? "Up" : "Down";
        return $"{direction} {CurrencyFormatter.Format(Math.Abs(amount))} this {period}";
    }

    /// <summary>
    /// Up as money in, down as money out, nothing when there's no change, see <see cref="MoneyColourClass"/>.
    /// </summary>
    public static string? ChangeClass(decimal? change)
    {
        if (change is not { } amount)
        {
            return null;
        }

        return MoneyColourClass.BySign(amount);
    }

    public static string KindIcon(NetWorthItemKind kind) => kind switch
    {
        NetWorthItemKind.Card => Icons.Material.Outlined.CreditCard,
        NetWorthItemKind.Debt => Icons.Material.Outlined.CreditScore,
        _ => Icons.Material.Outlined.AccountBalance
    };

    public static string KindName(NetWorthItemKind kind) => kind switch
    {
        NetWorthItemKind.Card => "Card",
        NetWorthItemKind.Debt => "Debt",
        _ => "Account"
    };

    /// <summary>
    /// Says which part of the chart is worked out, e.g. <c>The dashed line, before 3 Nov 2026, is worked out from your
    /// transactions, so approximate</c>, null when none of it is.
    /// </summary>
    public static string? DerivedNote(IReadOnlyList<BalancePoint> points)
    {
        BalancePoint? lastDerived = points.LastOrDefault(static x => x.IsDerived);
        if (lastDerived is null)
        {
            return null;
        }

        string synced = lastDerived.Date.AddDays(1).ToString("d MMM yyyy", CultureInfo.CurrentCulture);
        return $"The dashed line, before {synced}, is worked out from your transactions, so approximate";
    }

    /// <summary>
    /// e.g. <c>31 Oct 26</c>.
    /// </summary>
    public static string Day(DateOnly day) => day.ToString("d MMM yy", CultureInfo.CurrentCulture);
}
