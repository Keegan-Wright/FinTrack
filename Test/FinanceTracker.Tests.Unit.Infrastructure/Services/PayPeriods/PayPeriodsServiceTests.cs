using FinanceTracker.Contracts.PayPeriods;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.PayPeriods;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.PayPeriods;

public class PayPeriodsServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task GetYourPayAsync_NoPayYet_IsThisCalendarMonth()
    {
        // Arrange
        await SeedAsync();

        // Act
        YourPay pay = await BuildSubject().GetYourPayAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(pay.Pay).IsNull();
            await Assert.That(pay.FollowsPay).IsFalse();
            await Assert.That(pay.CurrentPeriod.Start).IsEqualTo(new DateOnly(2026, 11, 1));
            await Assert.That(pay.CurrentPeriod.End).IsEqualTo(new DateOnly(2026, 11, 30));
        }
    }

    [Test]
    public async Task GetYourPayAsync_PayPickedUp_IsFromTheLastPay()
    {
        // Arrange
        await SeedWithPayAsync();
        await SyncAsync();

        // Act
        YourPay pay = await BuildSubject().GetYourPayAsync(CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        string accountName = (await context.BankAccounts.SingleAsync(CancellationToken)).DisplayName;
        using (Assert.Multiple())
        {
            await Assert.That(pay.FollowsPay).IsTrue();
            await Assert.That(pay.IsChosen).IsFalse();
            await Assert.That(pay.Pay!.Name).IsEqualTo("ACME LTD");
            await Assert.That(pay.Pay.AccountName).IsEqualTo(accountName);
            await Assert.That(pay.Pay.Kind).IsEqualTo(PayDayKind.LastWorkingDay);
            await Assert.That(pay.Pay.LastPaidOn).IsEqualTo(new DateOnly(2026, 10, 30));
            await Assert.That(pay.CurrentPeriod.Start).IsEqualTo(new DateOnly(2026, 10, 30));
            await Assert.That(pay.CurrentPeriod.End).IsEqualTo(new DateOnly(2026, 11, 29));
            await Assert.That(pay.CurrentPeriod.Month).IsEqualTo(new DateOnly(2026, 11, 1));
        }
    }

    [Test]
    public async Task ChooseAsync_AnotherIncome_IsKeptAsThePay()
    {
        // Arrange
        await SeedWithPayAsync()
            .Do(s => s.TransactionScenario.WithTransaction("gig-aug", "main", Pay("SIDE GIG", 300m, 8, 14)))
            .Do(s => s.TransactionScenario.WithTransaction("gig-sep", "main", Pay("SIDE GIG", 300m, 9, 15)))
            .Do(s => s.TransactionScenario.WithTransaction("gig-oct", "main", Pay("SIDE GIG", 300m, 10, 15)));
        await SyncAsync();
        PayPeriodsService subject = BuildSubject();
        Income gig = await subject.GetIncomesAsync(CancellationToken)
            .SingleAsync(x => x.Name == "SIDE GIG", CancellationToken);

        // Act
        YourPay pay = await subject.ChooseAsync(new ChooseIncome { AccountId = gig.AccountId, Key = gig.Key },
            CancellationToken);
        await SyncAsync();

        // Assert
        YourPay after = await subject.GetYourPayAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(pay.IsChosen).IsTrue();
            await Assert.That(after.Pay!.Name).IsEqualTo("SIDE GIG");
            await Assert.That(after.CurrentPeriod.Start).IsEqualTo(new DateOnly(2026, 10, 15));
        }
    }

    [Test]
    public async Task ChooseAsync_NotComingInLikePay_Throws()
    {
        // Arrange
        await SeedWithPayAsync();

        // Act
        Func<Task> act = () => BuildSubject().ChooseAsync(
            new ChooseIncome { AccountId = Guid.NewGuid(), Key = "SOMEONE" }, CancellationToken);

        // Assert
        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task TurnOffAsync_ThenOn_GoesToCalendarMonthsAndBack()
    {
        // Arrange
        await SeedWithPayAsync();
        await SyncAsync();
        PayPeriodsService subject = BuildSubject();

        // Act
        YourPay off = await subject.TurnOffAsync(CancellationToken);
        YourPay on = await subject.TurnOnAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(off.IsOff).IsTrue();
            await Assert.That(off.FollowsPay).IsFalse();
            await Assert.That(off.CurrentPeriod.Start).IsEqualTo(new DateOnly(2026, 11, 1));
            await Assert.That(on.FollowsPay).IsTrue();
            await Assert.That(on.CurrentPeriod.Start).IsEqualTo(new DateOnly(2026, 10, 30));
        }
    }

    [Test]
    public async Task TurnOnAsync_NoPayYet_Throws()
    {
        // Arrange
        await SeedAsync();

        // Act
        Func<Task> act = () => BuildSubject().TurnOnAsync(CancellationToken);

        // Assert
        await Assert.That(act).Throws<InvalidOperationException>();
    }

    private Task<TestingScenario> SeedAsync() =>
        Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"));

    private Task<TestingScenario> SeedWithPayAsync() =>
        SeedAsync()
            .Do(s => s.TransactionScenario.WithTransaction("august", "main", Pay("ACME LTD", 2400m, 8, 28)))
            .Do(s => s.TransactionScenario.WithTransaction("september", "main", Pay("ACME LTD", 2400m, 9, 30)))
            .Do(s => s.TransactionScenario.WithTransaction("october", "main", Pay("ACME LTD", 2400m, 10, 30)));

    private static Domain.OpenBanking.Transactions.TransactionDetails Pay(string description, decimal amount,
        int month, int day) =>
        PaySyncServiceTests.Paid(description, amount, month, day);

    private Task<bool> SyncAsync() =>
        new PaySyncService(CurrentUserFor(Username), DbContextFactory, NullLogger<PaySyncService>.Instance,
            new FixedTimeProvider(Now)).SyncAsync(CancellationToken);

    private PayPeriodsService BuildSubject() =>
        new(CurrentUserFor(Username), DbContextFactory, NullLogger<PayPeriodsService>.Instance,
            new FixedTimeProvider(Now));
}
