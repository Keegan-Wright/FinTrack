using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Contracts.Account;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Contracts.NetWorth;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Tests.Unit.Client.PayPeriods;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using HomePage = FinanceTracker.Client.Pages.Home;

namespace FinanceTracker.Tests.Unit.Client.Home;

public class HomePageTests : ComponentTestBase
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private readonly IDashboardService _dashboardService = Substitute.For<IDashboardService>();
    private readonly IPayPeriodsService _payPeriodsService = Substitute.For<IPayPeriodsService>();
    private readonly IBudgetsService _budgetsService = Substitute.For<IBudgetsService>();
    private readonly ApplicationState _applicationState = new();

    public HomePageTests()
    {
        AddBunitPersistentComponentState();
        AddAuthorization().SetAuthorized("someone");

        IAccountService accountService = Substitute.For<IAccountService>();
        accountService.GetAccountsAndMostRecentTransactionsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<AccountAndTransactions>());
        ITransactionsService transactionsService = Substitute.For<ITransactionsService>();
        transactionsService.GetAllTransactionsAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<Transaction>());
        ISavingsGoalsService goalsService = Substitute.For<ISavingsGoalsService>();
        goalsService.GetSavingsGoalsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<SavingsGoal>());
        _budgetsService.GetMonthAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new BudgetMonthOverview());
        IHouseholdMemberService householdService = Substitute.For<IHouseholdMemberService>();
        householdService.GetHouseholdMembersAsync(Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<HouseholdMember>());
        INetWorthService netWorthService = Substitute.For<INetWorthService>();
        netWorthService.GetNetWorthAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(new NetWorthHistory());

        _dashboardService.GetSpentInTimePeriod(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new SpentInTimePeriod());
        _dashboardService.GetUpcomingPaymentsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<UpcomingPayment>());

        Services.AddSingleton(_dashboardService);
        Services.AddSingleton(accountService);
        Services.AddSingleton(transactionsService);
        Services.AddSingleton(goalsService);
        Services.AddSingleton(_budgetsService);
        Services.AddSingleton(_payPeriodsService);
        _payPeriodsService.GetYourPayAsync(Arg.Any<CancellationToken>()).Returns(YourPays.NoneYet(2026, 11));
        Services.AddSingleton(householdService);
        Services.AddSingleton(netWorthService);
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true));
    }

    [Test]
    public async Task Render_PayPickedUp_ThisPayPeriodFromPayDay()
    {
        // Arrange
        _payPeriodsService.GetYourPayAsync(Arg.Any<CancellationToken>()).Returns(YourPays.FromPayToPay());

        // Act
        IRenderedComponent<HomePage> cut = Render<HomePage>(p => p.AddCascadingValue(_applicationState));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindAll("button").Any(static x => x.TextContent.Trim() == "This pay period"))
                .IsTrue();
            await _dashboardService.Received(1).GetSpentInTimePeriod(new DateTime(2026, 10, 30), Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>());
            await _budgetsService.Received(1).GetMonthAsync(2026, 11, Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Synced_RefreshesTheChosenRange()
    {
        // Arrange
        IRenderedComponent<HomePage> cut = Render<HomePage>(p => p.AddCascadingValue(_applicationState));
        await cut.FindAll("button").Single(static x => x.TextContent.Trim() == "This year").ClickAsync(new());
        _dashboardService.ClearReceivedCalls();

        // Act
        await cut.InvokeAsync(_applicationState.NotifySyncedAsync);

        // Assert, this year's figures, not the month's it opened with
        await _dashboardService.Received(1).GetSpentInTimePeriod(new DateTime(2026, 1, 1), Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }
}
