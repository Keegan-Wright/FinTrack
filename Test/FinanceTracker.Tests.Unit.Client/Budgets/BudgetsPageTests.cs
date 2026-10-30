using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using BudgetsPage = FinanceTracker.Client.Pages.Budgets;

namespace FinanceTracker.Tests.Unit.Client.Budgets;

public class BudgetsPageTests : ComponentTestBase
{
    private static readonly DateTime Now = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

    private readonly IBudgetsService _budgetsService = Substitute.For<IBudgetsService>();
    private readonly ApplicationState _applicationState = new();

    public BudgetsPageTests()
    {
        AddBunitPersistentComponentState();
        IClassificationService classificationService = Substitute.For<IClassificationService>();
        classificationService.GetAllCustomClassificationsAsync(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<Classification>());

        Services.AddSingleton(_budgetsService);
        Services.AddSingleton(classificationService);
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));

        _budgetsService.GetBudgetsAsync(Arg.Any<CancellationToken>()).Returns(AsyncEnumerable.Empty<Budget>());
        _budgetsService.GetMonthAsync(2026, 3, Arg.Any<CancellationToken>()).Returns(Overview(3, current: true));
        _budgetsService.GetMonthAsync(2026, 2, Arg.Any<CancellationToken>()).Returns(Overview(2, current: false));
    }

    [Test]
    public async Task Render_ThisMonth_ShowsTheBudgetsAndWhatsUnbudgeted()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        IRenderedComponent<BudgetsPage> cut = RenderPage();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("March 2026");
            await Assert.That(cut.Markup).Contains("Groceries");
            await Assert.That(cut.Markup).Contains("Spent with no budget");
            await Assert.That(cut.FindAll("button[aria-label='Edit Groceries']")).Count().IsEqualTo(1);
        }
    }

    [Test]
    public async Task PreviousMonth_ShowsItAsItEnded()
    {
        // Arrange
        using CultureScope culture = new();
        IRenderedComponent<BudgetsPage> cut = RenderPage();

        // Act
        cut.Find("button[aria-label='Previous month']").Click();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("February 2026");
            await Assert.That(cut.Markup).Contains("As it ended");
            await Assert.That(cut.FindAll("button[aria-label='Edit Groceries']")).IsEmpty();
        }
    }

    [Test]
    public async Task NoBudgets_SaysHowToStart()
    {
        // Arrange
        _budgetsService.GetMonthAsync(2026, 3, Arg.Any<CancellationToken>())
            .Returns(new BudgetMonthOverview { Month = new DateOnly(2026, 3, 1), IsCurrent = true });

        // Act
        IRenderedComponent<BudgetsPage> cut = RenderPage();

        // Assert
        await Assert.That(cut.Markup).Contains("No budgets yet");
    }

    private IRenderedComponent<BudgetsPage> RenderPage() =>
        Render<BudgetsPage>(p => p.AddCascadingValue(_applicationState));

    private static BudgetMonthOverview Overview(int month, bool current) => new()
    {
        Month = new DateOnly(2026, month, 1),
        IsCurrent = current,
        Unbudgeted = 25m,
        Budgets =
        [
            new BudgetStatus
            {
                BudgetId = Guid.NewGuid(),
                Name = "Groceries",
                Limit = 400m,
                Available = 400m,
                Spent = 120m,
                Remaining = 280m,
                ExpectedByNow = 190m,
                OnPace = true
            }
        ]
    };
}
