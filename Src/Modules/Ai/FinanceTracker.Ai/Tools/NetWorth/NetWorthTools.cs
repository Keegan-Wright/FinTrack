using System.ComponentModel;
using FinanceTracker.Contracts.NetWorth;
using FinanceTracker.Contracts.Services;

namespace FinanceTracker.Ai.Tools.NetWorth;

/// <summary>
/// Tools for the user's net worth.
/// </summary>
/// <param name="netWorthService">Reads the net worth, the same as the net worth page.</param>
/// <param name="pageLinks">Makes the links to the pages its results can be checked on.</param>
public sealed class NetWorthTools(INetWorthService netWorthService, PageLinks pageLinks)
{
    /// <summary>
    /// How many months of history a result holds at most.
    /// </summary>
    private const int MostMonths = 24;

    /// <summary>
    /// The user's net worth now and month by month.
    /// </summary>
    /// <param name="from">The first day of history.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    [AiTool("get_net_worth")]
    [Description("Shows the user's net worth: today's, what makes it up, how it's changed this month and year, " +
        "and its value at the end of each month.")]
    public async Task<NetWorthSummary> GetNetWorthAsync(
        [Description("The first day of history, as yyyy-MM-dd. Leave out for all of it.")] DateOnly? from = null,
        CancellationToken cancellationToken = default)
    {
        NetWorthHistory history = await netWorthService.GetNetWorthAsync(from, cancellationToken);
        List<NetWorthMonth> months = MonthEnds(history.Points);
        NetWorthMonth? now = months.Count > 0 ? months[^1] : null;
        List<NetWorthPart> parts = history.Breakdown
            .Select(static x => new NetWorthPart(x.Name, x.Kind, x.Amount))
            .ToList();
        return new NetWorthSummary(now, history.ChangeThisMonth, history.ChangeThisYear, parts,
            months.TakeLast(MostMonths).ToList(), pageLinks.For(ToolLinks.NetWorth));
    }

    /// <summary>
    /// The last point of each month, oldest first.
    /// </summary>
    /// <param name="points">The history, a point a day or so.</param>
    private static List<NetWorthMonth> MonthEnds(List<NetWorthPoint> points)
    {
        List<NetWorthMonth> months = [];
        foreach (IGrouping<(int Year, int Month), NetWorthPoint> month in points
            .OrderBy(static x => x.Date)
            .GroupBy(static x => (x.Date.Year, x.Date.Month)))
        {
            NetWorthPoint last = month.Last();
            months.Add(new NetWorthMonth(last.Date, last.Assets, last.Liabilities, last.NetWorth));
        }

        return months;
    }
}
