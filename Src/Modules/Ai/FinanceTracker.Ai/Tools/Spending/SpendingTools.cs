using System.ComponentModel;
using FinanceTracker.Contracts.Reports.Account;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.Reports.SpentInTimePeriod;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Ai.Tools.Spending;

/// <summary>
/// Tools for how much the user spent and received.
/// </summary>
/// <param name="reportService">Works out the totals, the same as the reports pages.</param>
/// <param name="timeProvider">The clock, whose time zone the days are in.</param>
/// <param name="pageLinks">Makes the links to the pages its results can be checked on.</param>
public sealed class SpendingTools(IReportService reportService, TimeProvider timeProvider, PageLinks pageLinks)
{
    /// <summary>
    /// Money in and out over a period.
    /// </summary>
    /// <param name="from">The first day.</param>
    /// <param name="to">The last day.</param>
    /// <param name="accountIds">Only these accounts.</param>
    /// <param name="tags">Only these tags.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    [AiTool("get_totals")]
    [Description("Totals the money in and out over a period, optionally for some accounts or tags only. " +
        "Transfers between the user's own accounts aren't counted.")]
    public Task<PeriodTotals> GetTotalsAsync(
        [Description("The first day, as yyyy-MM-dd. Leave out for no start.")] DateOnly? from = null,
        [Description("The last day, as yyyy-MM-dd. Leave out for up to today.")] DateOnly? to = null,
        [Description("Only these accounts, ids from list_accounts. Leave out for all.")] List<Guid>? accountIds = null,
        [Description("Only transactions with one of these tags, from list_tags. Leave out for all.")]
        List<string>? tags = null,
        CancellationToken cancellationToken = default) =>
        TotalsAsync(from, to, accountIds, tags, cancellationToken);

    /// <summary>
    /// Money in and out over a period, split by tag or account.
    /// </summary>
    /// <param name="by">What to split it by.</param>
    /// <param name="from">The first day.</param>
    /// <param name="to">The last day.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    [AiTool("get_spending_breakdown")]
    [Description("Splits a period's money in and out by tag or by account, biggest spending first. Use it for " +
        "questions like what the user spends most on.")]
    public async Task<SpendingBreakdown> GetSpendingBreakdownAsync(
        [Description("Tag or Account.")] SpendingGrouping by,
        [Description("The first day, as yyyy-MM-dd. Leave out for no start.")] DateOnly? from = null,
        [Description("The last day, as yyyy-MM-dd. Leave out for up to today.")] DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        FilterTransactions filter = ToolFilters.For(timeProvider, from, to);
        List<SpendingGroup> groups = [];
        string link;
        if (by == SpendingGrouping.Tag)
        {
            link = pageLinks.For(ToolLinks.ReportByTag);
            await foreach (SpentInCategoryReport report in reportService
                .GetCategoryBreakdownReportAsync(filter, cancellationToken))
            {
                groups.Add(new SpendingGroup(report.Category, report.TotalIn, -report.TotalOut,
                    report.TotalTransactions));
            }
        }
        else
        {
            link = pageLinks.For(ToolLinks.ReportByAccount);
            await foreach (SpentInAccountReport report in reportService
                .GetAccountBreakdownReportAsync(filter, cancellationToken))
            {
                groups.Add(new SpendingGroup(report.AccountName, report.TotalIn, -report.TotalOut,
                    report.TotalTransactions));
            }
        }

        List<SpendingGroup> biggestFirst = groups
            .OrderByDescending(static x => x.MoneyOut)
            .Take(ToolText.MostRows)
            .ToList();
        return new SpendingBreakdown(from, to, by, biggestFirst, groups.Count > ToolText.MostRows, link);
    }

    /// <summary>
    /// Two periods' money in and out side by side.
    /// </summary>
    /// <param name="firstFrom">The first period's first day.</param>
    /// <param name="firstTo">The first period's last day.</param>
    /// <param name="secondFrom">The second period's first day.</param>
    /// <param name="secondTo">The second period's last day.</param>
    /// <param name="accountIds">Only these accounts.</param>
    /// <param name="tags">Only these tags.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    [AiTool("compare_periods")]
    [Description("Compares money in and out between two periods, optionally for some accounts or tags only. Use " +
        "it for questions like this year against last year.")]
    public async Task<PeriodComparison> ComparePeriodsAsync(
        [Description("The first period's first day, as yyyy-MM-dd.")] DateOnly firstFrom,
        [Description("The first period's last day, as yyyy-MM-dd.")] DateOnly firstTo,
        [Description("The second period's first day, as yyyy-MM-dd.")] DateOnly secondFrom,
        [Description("The second period's last day, as yyyy-MM-dd.")] DateOnly secondTo,
        [Description("Only these accounts, ids from list_accounts. Leave out for all.")] List<Guid>? accountIds = null,
        [Description("Only transactions with one of these tags, from list_tags. Leave out for all.")]
        List<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        PeriodTotals first = await TotalsAsync(firstFrom, firstTo, accountIds, tags, cancellationToken);
        PeriodTotals second = await TotalsAsync(secondFrom, secondTo, accountIds, tags, cancellationToken);
        return new PeriodComparison(first, second, second.MoneyIn - first.MoneyIn, second.MoneyOut - first.MoneyOut);
    }

    /// <summary>
    /// Money in and out over a period.
    /// </summary>
    /// <param name="from">The first day, null for no start.</param>
    /// <param name="to">The last day, null up to today.</param>
    /// <param name="accountIds">Only these accounts, null for all.</param>
    /// <param name="tags">Only these tags, null for all.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<PeriodTotals> TotalsAsync(DateOnly? from, DateOnly? to, List<Guid>? accountIds,
        List<string>? tags, CancellationToken cancellationToken)
    {
        FilterTransactions filter = ToolFilters.For(timeProvider, from, to, accountIds, tags);
        decimal moneyIn = 0;
        decimal moneyOut = 0;
        int transactions = 0;
        await foreach (SpentInTimePeriodReport report in reportService
            .GetSpentInTimePeriodReportAsync(filter, cancellationToken))
        {
            moneyIn += report.TotalIn;
            moneyOut -= report.TotalOut;
            transactions += report.TotalTransactions;
        }

        return new PeriodTotals(from, to, moneyIn, moneyOut, moneyIn - moneyOut, transactions,
            pageLinks.For(ToolLinks.ReportOverTime));
    }
}
