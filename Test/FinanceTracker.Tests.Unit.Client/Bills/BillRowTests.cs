using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client.Components.Bills;
using FinanceTracker.Contracts.Bills;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.Bills;

public class BillRowTests : ComponentTestBase
{
    private static readonly DateOnly Today = new(2026, 10, 30);

    [Test]
    public async Task Render_EachPeriodWithItsState()
    {
        // Arrange
        using CultureScope culture = new();
        List<BillPeriod> periods =
        [
            new() { Due = new DateOnly(2026, 10, 5), State = BillState.Paid, PaidOn = new DateOnly(2026, 10, 5) },
            new() { Due = new DateOnly(2026, 10, 12), State = BillState.Late },
            new()
            {
                Due = new DateOnly(2026, 10, 19), State = BillState.Changed, PaidOn = new DateOnly(2026, 10, 19),
                Expected = 5m, Paid = 6m
            }
        ];

        // Act
        IRenderedComponent<BillRow> cut = Render<BillRow>(p => p
            .Add(x => x.Bill, BillTextTests.Bill("Veg box"))
            .Add(x => x.Periods, periods)
            .Add(x => x.Today, Today));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindAll(".mud-chip").Select(static x => x.TextContent.Trim())).IsEquivalentTo(
                ["Paid 5 Oct", "Late, due 12 Oct", "Paid 19 Oct, price changed"]);
            await Assert.That(cut.Markup).Contains("Paid £6.00, expected £5.00");
            await Assert.That(cut.Markup).Contains("Current · Monthly · Direct debit");
        }
    }

    [Test]
    public async Task Render_NothingDueThisMonth_SaysSo()
    {
        // Act
        IRenderedComponent<BillRow> cut = Render<BillRow>(p => p
            .Add(x => x.Bill, BillTextTests.Bill())
            .Add(x => x.Periods, [])
            .Add(x => x.Today, Today));

        // Assert
        await Assert.That(cut.Markup).Contains("Not due this month");
    }

    [Test]
    public async Task Menu_FromADirectDebit_CanBeEndedButNotDeleted()
    {
        // Arrange
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<BillRow> cut = Render<BillRow>(p => p
            .Add(x => x.Bill, BillTextTests.Bill())
            .Add(x => x.Today, Today)
            .Add(x => x.Editable, true));

        // Act
        await cut.Find("button[aria-label='Actions for Energy']").ClickAsync(new());

        // Assert
        IReadOnlyList<string> items = popovers.WaitForElements(".mud-popover-open .mud-menu-item")
            .Select(static x => x.TextContent.Trim()).ToList();
        await Assert.That(items).IsEquivalentTo(["Edit", "End"]);
    }

    [Test]
    public async Task Menu_EndedManualBill_CanBeReopenedOrDeleted()
    {
        // Arrange
        bool reopened = false;
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<BillRow> cut = Render<BillRow>(p => p
            .Add(x => x.Bill, BillTextTests.Bill("Gym", endedOn: Today, source: BillSource.Manual))
            .Add(x => x.Today, Today)
            .Add(x => x.Editable, true)
            .Add(x => x.OnReopen, () => reopened = true));
        await cut.Find("button[aria-label='Actions for Gym']").ClickAsync(new());
        IReadOnlyList<IElement> items = popovers.WaitForElements(".mud-popover-open .mud-menu-item");
        List<string> labels = items.Select(static x => x.TextContent.Trim()).ToList();

        // Act
        await items.Single(x => x.TextContent.Trim() == "Reopen").ClickAsync(new());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(labels).IsEquivalentTo(["Edit", "Reopen", "Delete"]);
            await Assert.That(reopened).IsTrue();
        }
    }

    [Test]
    public async Task Render_NotEditable_HasNoMenu() =>
        await Assert.That(Render<BillRow>(p => p
                .Add(x => x.Bill, BillTextTests.Bill())
                .Add(x => x.Today, Today))
            .FindAll("[aria-label='Actions for Energy']")).IsEmpty();
}
