using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using SavingsGoalsPage = FinanceTracker.Client.Pages.SavingsGoals;

namespace FinanceTracker.Tests.Unit.Client.SavingsGoals;

public class SavingsGoalsPageTests : ComponentTestBase
{
    private readonly ISavingsGoalsService _goalsService = Substitute.For<ISavingsGoalsService>();
    private readonly ApplicationState _applicationState = new();

    public SavingsGoalsPageTests()
    {
        AddBunitPersistentComponentState();
        IClassificationService classificationService = Substitute.For<IClassificationService>();
        classificationService.GetAllCustomClassificationsAsync(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<Classification>());

        Services.AddSingleton(_goalsService);
        Services.AddSingleton(classificationService);

        _goalsService.GetSavingsAccountsAsync(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<SavingsGoalAccount>());
    }

    [Test]
    public async Task Render_ShowsEachGoalWithWhereItsSavedFrom()
    {
        // Arrange
        using CultureScope culture = new();
        _goalsService.GetSavingsGoalsAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new SavingsGoal
            {
                Id = Guid.NewGuid(),
                Name = "Car",
                Saved = 1300m,
                Target = 10000m,
                Source = SavingsGoalSource.Account,
                AccountName = "Rainy Day"
            }
        }.ToAsyncEnumerable());

        // Act
        IRenderedComponent<SavingsGoalsPage> cut = RenderPage();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("Car");
            await Assert.That(cut.Markup).Contains("£1,300.00 of £10,000.00");
            await Assert.That(cut.Markup).Contains("Balance of Rainy Day");
            await Assert.That(cut.FindAll("[aria-label='Actions for Car']")).Count().IsEqualTo(1);
        }
    }

    [Test]
    public async Task NoGoals_SaysHowToStart()
    {
        // Arrange
        _goalsService.GetSavingsGoalsAsync(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<SavingsGoal>());

        // Act
        IRenderedComponent<SavingsGoalsPage> cut = RenderPage();

        // Assert
        await Assert.That(cut.Markup).Contains("No goals yet");
    }

    [Test]
    [Arguments(SavingsGoalSource.Account, true)]
    [Arguments(SavingsGoalSource.Tags, true)]
    [Arguments(SavingsGoalSource.Manual, false)]
    public async Task Menu_OffersHistoryForGoalsThatTrackSomething(SavingsGoalSource source, bool offered)
    {
        // Arrange
        _goalsService.GetSavingsGoalsAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new SavingsGoal { Id = Guid.NewGuid(), Name = "Car", Source = source }
        }.ToAsyncEnumerable());
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<SavingsGoalsPage> cut = RenderPage();

        // Act
        IReadOnlyList<(string Text, bool Disabled)> items = await RowMenu.ItemsAsync(cut, popovers, "Car");

        // Assert
        await Assert.That(items.Any(static x => x.Text == "History")).IsEqualTo(offered);
    }

    private IRenderedComponent<SavingsGoalsPage> RenderPage() =>
        Render<SavingsGoalsPage>(p => p.AddCascadingValue(_applicationState));
}
