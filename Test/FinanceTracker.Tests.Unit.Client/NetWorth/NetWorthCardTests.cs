using Bunit;
using FinanceTracker.Client.Components.Dashboard;
using FinanceTracker.Contracts.NetWorth;

namespace FinanceTracker.Tests.Unit.Client.NetWorth;

public class NetWorthCardTests : ComponentTestBase
{
    [Test]
    public async Task Render_ShowsTodaysNetWorthAndTheChangeOverTheRange()
    {
        // Arrange
        using CultureScope culture = new();
        NetWorthHistory history = new()
        {
            Points =
            [
                new NetWorthPoint { Date = new DateOnly(2026, 10, 31), NetWorth = 1000m },
                new NetWorthPoint { Date = new DateOnly(2026, 11, 1), NetWorth = 950m }
            ]
        };

        // Act
        IRenderedComponent<NetWorthCard> cut = Render<NetWorthCard>(p => p
            .Add(x => x.History, history)
            .Add(x => x.From, new DateOnly(2026, 10, 31)));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("£950.00");
            await Assert.That(cut.Markup).Contains("Down £50.00 since 31 Oct");
            await Assert.That(cut.Markup).Contains("Since 31 Oct 2026");
            await Assert.That(cut.Find("a[href='NetWorth']")).IsNotNull();
        }
    }

    [Test]
    public async Task Render_NoHistory_SaysItBuildsUp() =>
        await Assert.That(Render<NetWorthCard>(p => p.Add(x => x.History, new NetWorthHistory())).Markup)
            .Contains("starts building up from your first sync");
}
