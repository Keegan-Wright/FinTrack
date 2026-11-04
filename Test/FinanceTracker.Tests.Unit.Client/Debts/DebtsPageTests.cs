using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using DebtsPage = FinanceTracker.Client.Pages.Debts;

namespace FinanceTracker.Tests.Unit.Client.Debts;

public class DebtsPageTests : ComponentTestBase
{
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    private readonly IDebtsService _debtsService = Substitute.For<IDebtsService>();
    private readonly ApplicationState _applicationState = new();

    public DebtsPageTests()
    {
        AddBunitPersistentComponentState();
        ITransactionsService transactionsService = Substitute.For<ITransactionsService>();
        transactionsService.GetAccountsForTransactionFiltersAsync(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<TransactionAccountFilter>());

        Services.AddSingleton(_debtsService);
        Services.AddSingleton(transactionsService);
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
    }

    [Test]
    public async Task Render_Debts_ShowsTheTotalsAndEachDebt()
    {
        // Arrange
        using CultureScope culture = new();
        Debts(
            DebtTextTests.Debt("Car loan", owed: 900m,
                forecast: DebtTextTests.Paying(4, new DateOnly(2027, 3, 2))),
            DebtTextTests.Debt("Sofa", owed: 300m,
                forecast: DebtTextTests.Paying(2, new DateOnly(2027, 1, 2))));

        // Act
        IRenderedComponent<DebtsPage> cut = RenderPage();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("£1,200.00");
            await Assert.That(cut.Markup).Contains("Across 2 debts");
            await Assert.That(cut.Markup).Contains("£500.00");
            await Assert.That(cut.Markup).Contains("Debt free by Mar 2027");
            await Assert.That(cut.FindAll("a[href^='Debts/']")).Count().IsEqualTo(2);
            await Assert.That(cut.FindAll("[aria-label='Actions for Car loan']")).Count().IsEqualTo(1);
        }
    }

    [Test]
    public async Task Render_OneNeverPaidOff_SaysSoRatherThanADate()
    {
        // Arrange
        Debts(DebtTextTests.Debt("Card", forecast: new DebtForecast()));

        // Act
        IRenderedComponent<DebtsPage> cut = RenderPage();

        // Assert
        await Assert.That(cut.Markup).Contains("One isn't paid off at its payment");
    }

    [Test]
    public async Task Render_PaidOffDebt_ListedOnItsOwn()
    {
        // Arrange
        Debts(DebtTextTests.Debt("Car loan"), DebtTextTests.Debt("Old phone", owed: 0m, paidOffPercent: 100m,
            forecast: DebtTextTests.Paying(0, new DateOnly(2026, 11, 2))));

        // Act
        IRenderedComponent<DebtsPage> cut = RenderPage();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("Paid off");
            await Assert.That(cut.Markup).Contains("Nothing left to pay on these");
            await Assert.That(cut.Markup).Contains("Old phone");
        }
    }

    [Test]
    public async Task Render_NoDebts_SaysWhatToAdd()
    {
        // Arrange
        Debts();

        // Act
        IRenderedComponent<DebtsPage> cut = RenderPage();

        // Assert
        await Assert.That(cut.Markup).Contains("No debts yet");
    }

    [Test]
    public async Task Synced_ShowsWhatsOwedNow()
    {
        // Arrange
        using CultureScope culture = new();
        Debts(DebtTextTests.Debt("Car loan", owed: 900m, forecast: DebtTextTests.Paying(4, new DateOnly(2027, 3, 2))));
        IRenderedComponent<DebtsPage> cut = RenderPage();
        Debts(DebtTextTests.Debt("Car loan", owed: 650m, forecast: DebtTextTests.Paying(3, new DateOnly(2027, 2, 2))));

        // Act
        await cut.InvokeAsync(_applicationState.NotifySyncedAsync);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("£650.00");
            await Assert.That(cut.Markup).Contains("Debt free by Feb 2027");
        }
    }

    private void Debts(params Debt[] debts) =>
        _debtsService.GetDebtsAsync(Arg.Any<CancellationToken>()).Returns(_ => debts.ToAsyncEnumerable());

    private IRenderedComponent<DebtsPage> RenderPage() =>
        Render<DebtsPage>(p => p.AddCascadingValue(_applicationState));
}
