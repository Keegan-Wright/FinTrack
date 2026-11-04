using System.Globalization;
using FinanceTracker.Client.Components.PayPeriods;
using FinanceTracker.Contracts.Reports;
using FinanceTracker.Contracts.Reports.Account;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.Reports.SpentInTimePeriod;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Client.Components.Reports;

/// <summary>
/// Maps the server's reports onto <see cref="ReportNode"/> trees, all time down to days. Months and days without transactions are filled in with zeros.
/// </summary>
public static class ReportTree
{
    public static ReportNode From(SpentInTimePeriodReport report, FilterTransactions request) =>
        Root("All accounts", report, request, report.YearlyBreakdown.Select(static y => Year(y.Year, y,
            y.MonthlyBreakdown.Select(m => Month(y.Year, m.Month, m.Start, m.End, m,
                m.DailyBreakdown.Select(static d => (d.Date, (IReport)d)))))));

    public static ReportNode From(SpentInAccountReport report, FilterTransactions request) =>
        Root(report.AccountName, report, Narrow(request, accountIds: [report.AccountId]),
            report.YearlyBreakdown.Select(static y => Year(y.Year, y,
                y.MonthlyBreakdown.Select(m => Month(y.Year, m.Month, m.Start, m.End, m,
                    m.DailyBreakdown.Select(static d => (d.Date, (IReport)d)))))), report.AccountId);

    /// <summary>
    /// An account with no transactions matching the filters.
    /// </summary>
    public static ReportNode EmptyAccount(Guid accountId, string name) => new()
    {
        Label = name,
        Title = name,
        ChildPeriod = "year",
        AccountId = accountId,
        IncludesTransfers = true
    };

    public static ReportNode From(SpentInCategoryReport report, FilterTransactions request) =>
        Root(report.Category, report, Narrow(request, tags: [report.Category]),
            report.YearlyBreakdown.Select(static y => Year(y.Year, y,
                y.MonthlyBreakdown.Select(m => Month(y.Year, m.Month, m.Start, m.End, m,
                    m.DailyBreakdown.Select(static d => (d.Date, (IReport)d)))))));

    /// <summary>
    /// The accounts the filters cover with nothing in them. Bank
    /// filters can't be matched without each account's bank, so none are added when one is chosen.
    /// </summary>
    public static IEnumerable<ReportNode> EmptyAccounts(IEnumerable<ReportNode> reported,
        IEnumerable<TransactionAccountFilter> accounts, FilterTransactions request)
    {
        if (request.ProviderIds is { Count: > 0 })
        {
            return [];
        }

        HashSet<Guid?> reportedIds = reported.Select(static x => x.AccountId).ToHashSet();
        return accounts
            .Where(x => request.AccountIds is not { Count: > 0 } || request.AccountIds.Contains(x.AccountId))
            .Where(x => !reportedIds.Contains(x.AccountId))
            .Select(static x => EmptyAccount(x.AccountId, x.AccountName))
            .ToList();
    }

    /// <summary>
    /// The request listing the transactions behind one day of a report.
    /// </summary>
    public static FilterTransactions ForDay(FilterTransactions scope, DateTime day) =>
        Narrow(scope, from: day.Date, to: day.Date.AddDays(1).AddTicks(-1));

    // The report's own filters still apply, a narrower account, tag or date replaces the chosen ones
    private static FilterTransactions Narrow(FilterTransactions request, IReadOnlyList<Guid>? accountIds = null,
        IReadOnlyList<string>? tags = null, DateTime? from = null, DateTime? to = null) => new()
    {
        AccountIds = accountIds ?? request.AccountIds,
        ProviderIds = request.ProviderIds,
        Categories = request.Categories,
        Types = request.Types,
        SearchTerm = request.SearchTerm,
        FromDate = from ?? request.FromDate,
        ToDate = to ?? request.ToDate,
        Tags = tags ?? request.Tags,
        BillIds = request.BillIds,
        DebtIds = request.DebtIds,
        SavingsGoalIds = request.SavingsGoalIds
    };

    // An account report counts transfers, see ReportNode.IncludesTransfers
    private static ReportNode Root(string name, IReport totals, FilterTransactions scope,
        IEnumerable<(int Year, ReportNode Node)> years, Guid? accountId = null)
    {
        List<ReportNode> children = years.OrderBy(static x => x.Year).Select(static x => x.Node).ToList();
        return new ReportNode
        {
            Label = name,
            Title = name,
            TotalTransactions = totals.TotalTransactions,
            TotalIn = totals.TotalIn,
            TotalOut = totals.TotalOut,
            Days = children.Sum(static x => x.Days),
            Children = children,
            ChildPeriod = "year",
            Scope = scope,
            AccountId = accountId,
            IncludesTransfers = accountId is not null
        };
    }

    private static (int Year, ReportNode Node) Year(int year, IReport totals,
        IEnumerable<(int Month, ReportNode Node)> months)
    {
        Dictionary<int, ReportNode> byMonth = [];
        foreach ((int month, ReportNode node) in months)
        {
            byMonth.TryAdd(month, node);
        }

        List<ReportNode> children = Enumerable.Range(1, 12)
            .Select(m => byMonth.GetValueOrDefault(m) ?? EmptyMonth(year, m))
            .ToList();
        string label = year.ToString(CultureInfo.CurrentCulture);
        return (year, new ReportNode
        {
            Label = label,
            Title = label,
            TotalTransactions = totals.TotalTransactions,
            TotalIn = totals.TotalIn,
            TotalOut = totals.TotalOut,
            Days = children.Sum(static x => x.Days),
            Children = children,
            ChildPeriod = "month"
        });
    }

    /// <param name="start">The first day of the period the month covers, from pay to pay or the calendar month.
    /// </param>
    /// <param name="end">Its last day, inclusive.</param>
    private static (int Month, ReportNode Node) Month(int year, string monthName, DateOnly start, DateOnly end,
        IReport totals, IEnumerable<(DateOnly Date, IReport Totals)> days)
    {
        int month = MonthNumber(monthName);
        Dictionary<DateOnly, IReport> byDay = [];
        foreach ((DateOnly date, IReport dayTotals) in days)
        {
            byDay.TryAdd(date, dayTotals);
        }

        List<ReportNode> dayNodes = [];
        for (DateOnly date = start; date <= end; date = date.AddDays(1))
        {
            dayNodes.Add(Day(date.ToDateTime(TimeOnly.MinValue), byDay.GetValueOrDefault(date)));
        }

        return (month, MonthNode(year, month, start, end, totals, dayNodes));
    }

    private static ReportNode EmptyMonth(int year, int month)
    {
        DateOnly first = new(year, month, 1);
        return MonthNode(year, month, first, first.AddMonths(1).AddDays(-1), null, []);
    }

    private static ReportNode MonthNode(int year, int month, DateOnly start, DateOnly end, IReport? totals,
        IReadOnlyList<ReportNode> days)
    {
        DateTime named = new(year, month, 1);
        return new ReportNode
        {
            Label = named.ToString("MMM", CultureInfo.CurrentCulture),
            Title = named.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            Dates = PeriodDates(year, month, start, end),
            TotalTransactions = totals?.TotalTransactions ?? 0,
            TotalIn = totals?.TotalIn ?? 0,
            TotalOut = totals?.TotalOut ?? 0,
            Days = end.DayNumber - start.DayNumber + 1,
            Children = days,
            ChildPeriod = "day"
        };
    }

    /// <summary>
    /// e.g. <c>30 Oct – 29 Nov</c> for a month that runs from pay to pay, null for a calendar month.
    /// </summary>
    private static string? PeriodDates(int year, int month, DateOnly start, DateOnly end)
    {
        DateOnly first = new(year, month, 1);
        if (start == first && end == first.AddMonths(1).AddDays(-1))
        {
            return null;
        }

        return PayText.Period(start, end);
    }

    private static ReportNode Day(DateTime date, IReport? totals) => new()
    {
        Label = date.Day.ToString(CultureInfo.CurrentCulture),
        Title = date.ToString("ddd d MMMM", CultureInfo.CurrentCulture),
        TotalTransactions = totals?.TotalTransactions ?? 0,
        TotalIn = totals?.TotalIn ?? 0,
        TotalOut = totals?.TotalOut ?? 0,
        Days = 1,
        Date = date
    };

    // The server names months in its culture
    private static int MonthNumber(string monthName)
    {
        int index = Array.FindIndex(CultureInfo.CurrentCulture.DateTimeFormat.MonthNames,
            x => string.Equals(x, monthName, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index + 1 : 1;
    }
}
