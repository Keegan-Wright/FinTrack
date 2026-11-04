using Bunit;
using FinanceTracker.Client.Components.Reports;
using FinanceTracker.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Client.Reports;

public class ReportDrilldownTests : ComponentTestBase
{
    public ReportDrilldownTests() => Services.AddSingleton(Substitute.For<ITransactionsService>());

    [Test]
    public async Task Refreshed_TheSameReport_StaysWhereTheUserWas()
    {
        // Arrange
        IRenderedComponent<ReportDrilldown> cut = Render<ReportDrilldown>(p => p.Add(x => x.Root, Groceries()));
        await OpenAsync(cut, "2026");
        await OpenAsync(cut, "October 2026");

        // Act
        cut.Render(p => p.Add(x => x.Root, Groceries()));

        // Assert
        await Assert.That(Trail(cut)).IsEquivalentTo(["Groceries", "2026", "October 2026"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Refreshed_WithoutThePeriodTheUserWasIn_GoesBackUpToWhatsLeft()
    {
        // Arrange
        IRenderedComponent<ReportDrilldown> cut = Render<ReportDrilldown>(p => p.Add(x => x.Root, Groceries()));
        await OpenAsync(cut, "2026");
        await OpenAsync(cut, "October 2026");

        // Act
        cut.Render(p => p.Add(x => x.Root, Groceries(withOctober: false)));

        // Assert
        await Assert.That(Trail(cut)).IsEquivalentTo(["Groceries", "2026"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task AnotherReport_StartsAtTheTop()
    {
        // Arrange
        IRenderedComponent<ReportDrilldown> cut = Render<ReportDrilldown>(p => p.Add(x => x.Root, Groceries()));
        await OpenAsync(cut, "2026");

        // Act
        cut.Render(p => p.Add(x => x.Root, Groceries(title: "Household")));

        // Assert
        await Assert.That(Trail(cut)).IsEquivalentTo(["Household"]);
    }

    /// <summary>
    /// Clicks the period's row, found again right before the click as the chart re-renders the list once drawn.
    /// </summary>
    private static async Task OpenAsync(IRenderedComponent<ReportDrilldown> cut, string period)
    {
        await cut.InvokeAsync(() => cut.FindAll(".ft-list__row--link")
            .First(x => x.TextContent.Contains(period, StringComparison.Ordinal))
            .Click());
        cut.WaitForState(() => cut.Find("nav.ft-trail").TextContent.Contains(period, StringComparison.Ordinal));
    }

    private static List<string> Trail(IRenderedComponent<ReportDrilldown> cut) =>
        cut.Find("nav.ft-trail").Children
            .Where(static x => x.TagName is "BUTTON" or "SPAN")
            .Select(static x => x.TextContent.Trim())
            .Where(static x => x.Length > 0)
            .ToList();

    /// <summary>
    /// Two years, so the drill-down doesn't start inside a lone one, and 2026 with October and November.
    /// </summary>
    private static ReportNode Groceries(bool withOctober = true, string title = "Groceries")
    {
        List<ReportNode> months = [Month("November 2026")];
        if (withOctober)
        {
            months.Insert(0, Month("October 2026"));
        }

        return Period(title, "year",
        [
            Period("2025", "month", [Month("March 2025")]),
            Period("2026", "month", months)
        ]);
    }

    /// <summary>
    /// A month with a day in it, so it can be opened.
    /// </summary>
    private static ReportNode Month(string title) => Period(title, "day", [Period($"1 {title}", null)]);

    private static ReportNode Period(string title, string? childPeriod, IReadOnlyList<ReportNode>? children = null) =>
        new()
        {
            Label = title,
            Title = title,
            TotalTransactions = 2,
            TotalOut = -20m,
            Days = 30,
            ChildPeriod = childPeriod,
            Children = children ?? []
        };
}
