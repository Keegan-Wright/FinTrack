using Bunit;
using FinanceTracker.Client.Components.Dashboard;
using FinanceTracker.Contracts.Dashboard;

namespace FinanceTracker.Tests.Unit.Client.Dashboard;

public class DashboardKpisTests : ComponentTestBase
{
    [Test]
    public async Task Render_TransfersLeftOut_SaysHowMuch()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        IRenderedComponent<DashboardKpis> cut = Render<DashboardKpis>(p => p.Add(x => x.Spent, new SpentInTimePeriod
        {
            TotalIn = 100m, TotalOut = -50m, TransferredIn = 300m, TransferredOut = -450.5m
        }));

        // Assert
        await Assert.That(cut.Find(".ft-kpis__transfers").TextContent.Trim())
            .IsEqualTo("Leaves out £300.00 in and £450.50 out moved between your accounts or household");
    }

    [Test]
    public async Task Render_NoTransfers_LeavesTheCaptionOut()
    {
        // Act
        IRenderedComponent<DashboardKpis> cut = Render<DashboardKpis>(p => p.Add(x => x.Spent, new SpentInTimePeriod
        {
            TotalIn = 100m, TotalOut = -50m
        }));

        // Assert
        await Assert.That(cut.FindAll(".ft-kpis__transfers")).IsEmpty();
    }

    [Test]
    [Arguments(1, "1 transaction in range")]
    [Arguments(0, "0 transactions in range")]
    [Arguments(1234, "1,234 transactions in range")]
    public async Task Render_TransactionCount_ReadsRightForHowMany(int count, string expected)
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        IRenderedComponent<DashboardKpis> cut = Render<DashboardKpis>(p => p
            .Add(x => x.Spent, new SpentInTimePeriod())
            .Add(x => x.TransactionCount, count));

        // Assert
        await Assert.That(cut.Markup).Contains(expected);
    }
}
