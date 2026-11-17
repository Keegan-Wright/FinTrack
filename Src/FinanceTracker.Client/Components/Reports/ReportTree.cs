using System.Globalization;
using FinanceTracker.Client.Components.Common;
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
    /// <summary>
    /// The all accounts report as a tree.
    /// </summary>
    /// <param name="report">The server's report.</param>
    /// <param name="request">The filters it was asked for with.</param>
    public static ReportNode From(SpentInTimePeriodReport report, FilterTransactions request) =>
        Root("All accounts", report, request, report.YearlyBreakdown.Select(static y => Year(y.Year, y,
            y.MonthlyBreakdown.Select(m => Month(y.Year, m.Month, m.Start, m.End, m,
                m.DailyBreakdown.Select(static d => (d.Date, (IReport)d)))))));

    /// <summary>
    /// An account's report as a tree, narrowed to the account.
    /// </summary>
    /// <param name="report">The server's report.</param>
    /// <param name="request">The filters it was asked for with.</param>
    public static ReportNode From(SpentInAccountReport report, FilterTransactions request) =>
        Root(report.AccountName, report, Narrow(request, accountIds: [report.AccountId]),
            report.YearlyBreakdown.Select(static y => Year(y.Year, y,
                y.MonthlyBreakdown.Select(m => Month(y.Year, m.Month, m.Start, m.End, m,
                    m.DailyBreakdown.Select(static d => (d.Date, (IReport)d)))))), report.AccountId);

    /// <summary>
    /// An account with no transactions matching the filters.
    /// </summary>
    /// <param name="accountId">The account's id.</param>
    /// <param name="name">The account's name.</param>
    public static ReportNode EmptyAccount(Guid accountId, string name) => new()
    {
        Label = name,
        Title = name,
        ChildPeriod = "year",
        AccountId = accountId,
        IncludesTransfers = true
    };

    /// <summary>
    /// A tag's report as a tree, narrowed to the tag.
    /// </summary>
    /// <param name="report">The server's report.</param>
    /// <param name="request">The filters it was asked for with.</param>
    public static ReportNode From(SpentInCategoryReport report, FilterTransactions request) =>
        Root(report.Category, report, Narrow(request, tags: [report.Category]),
            report.YearlyBreakdown.Select(static y => Year(y.Year, y,
                y.MonthlyBreakdown.Select(m => Month(y.Year, m.Month, m.Start, m.End, m,
                    m.DailyBreakdown.Select(static d => (d.Date, (IReport)d)))))));

    /// <summary>
    /// The accounts the filters cover with nothing in them. Bank
    /// filters can't be matched without each account's bank, so none are added when one is chosen.
    /// </summary>
    /// <param name="reported">The account reports the server returned.</param>
    /// <param name="accounts">The user's accounts, as the filters offer them.</param>
    /// <param name="request">The filters.</param>
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
    /// The request listing the transactions behind one day of a report, the day where the user is
    /// (<paramref name="timeZone"/>) as the report counts it.
    /// </summary>
    /// <param name="scope">The node's filters.</param>
    /// <param name="day">The day.</param>
    /// <param name="timeZone">Where the user is.</param>
    public static FilterTransactions ForDay(FilterTransactions scope, DateTime day, TimeZoneInfo timeZone) =>
        Narrow(scope, from: LocalDays.StartUtc(day, timeZone), to: LocalDays.EndUtc(day, timeZone));

    // The report's own filters still apply, a narrower account, tag or date replaces the chosen ones
    /// <summary>
    /// <paramref name="request"/> with whatever's given replacing what it had.
    /// </summary>
    /// <param name="request">The filters.</param>
    /// <param name="accountIds">The accounts, null to keep the filters'.</param>
    /// <param name="tags">The tags, null to keep the filters'.</param>
    /// <param name="from">The first day, null to keep the filters'.</param>
    /// <param name="to">The last day, null to keep the filters'.</param>
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
    /// <summary>
    /// The report's top node, all time, with its years in order.
    /// </summary>
    /// <param name="name">What the report is of.</param>
    /// <param name="totals">The report's totals.</param>
    /// <param name="scope">The filters behind it.</param>
    /// <param name="years">Its years.</param>
    /// <param name="accountId">The account an account report is for.</param>
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

    /// <summary>
    /// A year's node, with every month in it.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="totals">The year's totals.</param>
    /// <param name="months">Its months that have transactions.</param>
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

    /// <param name="year">The year.</param>
    /// <param name="monthName">The month's name.</param>
    /// <param name="start">The first day of the period the month covers, from pay to pay or the calendar month.
    /// </param>
    /// <param name="end">Its last day, inclusive.</param>
    /// <param name="totals">The month's totals.</param>
    /// <param name="days">The month's days and their totals.</param>
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

    /// <summary>
    /// A calendar month with no transactions.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month of the year, 1 to 12.</param>
    private static ReportNode EmptyMonth(int year, int month)
    {
        DateOnly first = new(year, month, 1);
        return MonthNode(year, month, first, first.AddMonths(1).AddDays(-1), null, []);
    }

    /// <summary>
    /// A month's node.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month of the year, 1 to 12.</param>
    /// <param name="start">The first day it covers.</param>
    /// <param name="end">The last day it covers.</param>
    /// <param name="totals">Its totals, null for none.</param>
    /// <param name="days">Its days.</param>
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
    /// <param name="year">The year.</param>
    /// <param name="month">The month of the year, 1 to 12.</param>
    /// <param name="start">The period's first day.</param>
    /// <param name="end">The period's last day.</param>
    private static string? PeriodDates(int year, int month, DateOnly start, DateOnly end)
    {
        DateOnly first = new(year, month, 1);
        if (start == first && end == first.AddMonths(1).AddDays(-1))
        {
            return null;
        }

        return PayText.Period(start, end);
    }

    /// <summary>
    /// A day's node.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <param name="totals">Its totals, null for none.</param>
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
    /// <summary>
    /// The month a name is, in the current culture, January when it's none.
    /// </summary>
    /// <param name="monthName">The month's name.</param>
    private static int MonthNumber(string monthName)
    {
        int index = Array.FindIndex(CultureInfo.CurrentCulture.DateTimeFormat.MonthNames,
            x => string.Equals(x, monthName, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index + 1 : 1;
    }
}
