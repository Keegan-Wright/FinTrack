using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.PayPeriods;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Tests.Unit.Client.PayPeriods;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using BudgetsPage = FinanceTracker.Client.Pages.Budgets;

namespace FinanceTracker.Tests.Unit.Client.Budgets;

public class BudgetsPageTests : ComponentTestBase
{
    private static readonly DateTime Now = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

    private readonly IBudgetsService _budgetsService = Substitute.For<IBudgetsService>();
    private readonly IPayPeriodsService _payPeriodsService = Substitute.For<IPayPeriodsService>();
    private readonly ApplicationState _applicationState = new();

    public BudgetsPageTests()
    {
        AddBunitPersistentComponentState();
        IClassificationService classificationService = Substitute.For<IClassificationService>();
        classificationService.GetAllCustomClassificationsAsync(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<Classification>());

        Services.AddSingleton(_budgetsService);
        Services.AddSingleton(_payPeriodsService);
        _payPeriodsService.GetYourPayAsync(Arg.Any<CancellationToken>()).Returns(YourPays.NoneYet(2026, 3));
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
            await Assert.That(cut.FindAll("[aria-label='Actions for Groceries']")).Count().IsEqualTo(1);
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
            await Assert.That(cut.FindAll("[aria-label='Actions for Groceries']")).IsEmpty();
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

    [Test]
    public async Task Synced_RefreshesTheMonthShown()
    {
        // Arrange
        using CultureScope culture = new();
        IRenderedComponent<BudgetsPage> cut = RenderPage();
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Previous month']").Click());
        cut.WaitForState(() => cut.Markup.Contains("February 2026"));

        // Act
        await cut.InvokeAsync(_applicationState.NotifySyncedAsync);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("February 2026");
            await _budgetsService.Received(2).GetMonthAsync(2026, 2, Arg.Any<CancellationToken>());
            await _budgetsService.Received(1).GetMonthAsync(2026, 3, Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Render_PayPickedUp_OpensOnThePayPeriodWithItsDates()
    {
        // Arrange
        using CultureScope culture = new();
        PaidFromPayToPay();

        // Act
        IRenderedComponent<BudgetsPage> cut = RenderPage();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("November 2026");
            await Assert.That(cut.Markup).Contains("30 Oct – 29 Nov");
            await Assert.That(cut.Markup).Contains("From pay to pay: ACME LTD");
        }
    }

    [Test]
    public async Task UseCalendarMonths_TurnsPayPeriodsOffAndShowsTheMonthAgain()
    {
        // Arrange
        using CultureScope culture = new();
        PaidFromPayToPay();
        _payPeriodsService.TurnOffAsync(Arg.Any<CancellationToken>()).Returns(YourPays.FromPayToPay(off: true));
        Render<MudSnackbarProvider>();
        IRenderedComponent<BudgetsPage> cut = RenderPage();

        // Act
        await cut.InvokeAsync(() => cut.FindAll("button")
            .Single(static x => x.TextContent.Trim() == "Use calendar months").Click());

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Use pay periods"));
        using (Assert.Multiple())
        {
            await _payPeriodsService.Received(1).TurnOffAsync(Arg.Any<CancellationToken>());
            await _budgetsService.Received(2).GetMonthAsync(2026, 11, Arg.Any<CancellationToken>());
            await Assert.That(cut.Markup).DoesNotContain("30 Oct – 29 Nov");
        }
    }

    [Test]
    public async Task ChooseAnotherIncome_SavesTheOneChosen()
    {
        // Arrange
        PaidFromPayToPay();
        YourPay pay = YourPays.FromPayToPay();
        _payPeriodsService.GetIncomesAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new[] { pay.Pay! }.ToAsyncEnumerable());
        _payPeriodsService.ChooseAsync(Arg.Any<ChooseIncome>(), Arg.Any<CancellationToken>()).Returns(pay);
        Render<MudPopoverProvider>();
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        Render<MudSnackbarProvider>();
        IRenderedComponent<BudgetsPage> cut = RenderPage();

        // Act
        await cut.InvokeAsync(() => cut.FindAll("button")
            .Single(static x => x.TextContent.Trim() == "Choose another income").Click());
        await dialogs.WaitForElements("button").Single(static x => x.TextContent.Trim() == "OK")
            .ClickAsync(new MouseEventArgs());

        // Assert
        cut.WaitForAssertion(() => _payPeriodsService.Received(1).ChooseAsync(
            Arg.Is<ChooseIncome>(x => x.Key == "ACMELTD"), Arg.Any<CancellationToken>()));
        await Assert.That(cut.Markup).Contains("From pay to pay: ACME LTD");
    }

    private void PaidFromPayToPay()
    {
        _payPeriodsService.GetYourPayAsync(Arg.Any<CancellationToken>()).Returns(YourPays.FromPayToPay());
        _budgetsService.GetMonthAsync(2026, 11, Arg.Any<CancellationToken>()).Returns(new BudgetMonthOverview
        {
            Month = new DateOnly(2026, 11, 1),
            Start = new DateOnly(2026, 10, 30),
            End = new DateOnly(2026, 11, 29),
            IsCurrent = true
        });
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
