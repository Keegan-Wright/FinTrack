using Bunit;
using FinanceTracker.Client.Components.Budgets;
using FinanceTracker.Contracts.Budgets;

namespace FinanceTracker.Tests.Unit.Client.Budgets;

public class BudgetRowTests : ComponentTestBase
{
    [Test]
    public async Task Render_ThisMonth_ShowsThePaceAndCanBeEdited()
    {
        // Arrange
        using CultureScope culture = new();
        bool edited = false;

        // Act
        IRenderedComponent<BudgetRow> cut = Render<BudgetRow>(p => p
            .Add(x => x.Status, Status(spent: 160m, expected: 120m, pending: 12m))
            .Add(x => x.ShowPace, true)
            .Add(x => x.Editable, true)
            .Add(x => x.OnEdit, () => edited = true));
        cut.Find("button[aria-label='Edit Groceries']").Click();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("£240.00 left");
            await Assert.That(cut.Markup).Contains("£40.00 ahead of pace");
            await Assert.That(cut.Markup).Contains("£12.00 still pending");
            await Assert.That(cut.FindAll(".ft-budget__pace")).Count().IsEqualTo(1);
            await Assert.That(edited).IsTrue();
        }
    }

    [Test]
    public async Task Render_PastMonth_HasNoPaceOrButtons()
    {
        // Act
        IRenderedComponent<BudgetRow> cut = Render<BudgetRow>(p => p
            .Add(x => x.Status, Status(spent: 430m, expected: 400m)));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindAll(".ft-budget__pace")).IsEmpty();
            await Assert.That(cut.FindAll("button")).IsEmpty();
            await Assert.That(cut.Markup).DoesNotContain("pace");
        }
    }

    [Test]
    public async Task Render_TagsDeleted_SaysToChooseOne()
    {
        // Arrange
        BudgetStatus status = Status(spent: 0m, expected: 0m, coversNothing: true);

        // Act
        IRenderedComponent<BudgetRow> cut = Render<BudgetRow>(p => p.Add(x => x.Status, status));

        // Assert
        await Assert.That(cut.Markup).Contains("Its tags were deleted, edit it to choose one");
    }

    [Test]
    public async Task Render_BarSaysWhatItShows()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        IRenderedComponent<BudgetRow> cut = Render<BudgetRow>(p => p.Add(x => x.Status, Status(120m, 0m)));

        // Assert
        await Assert.That(cut.Find("[aria-label='Groceries, £120.00 of £400.00 spent']")).IsNotNull();
    }

    private static BudgetStatus Status(decimal spent, decimal expected, decimal pending = 0,
        bool coversNothing = false) => new()
    {
        CoversNothing = coversNothing,
        BudgetId = Guid.NewGuid(),
        Name = "Groceries",
        Limit = 400m,
        Available = 400m,
        Spent = spent,
        Pending = pending,
        Remaining = 400m - spent,
        ExpectedByNow = expected,
        OnPace = spent <= expected
    };
}
