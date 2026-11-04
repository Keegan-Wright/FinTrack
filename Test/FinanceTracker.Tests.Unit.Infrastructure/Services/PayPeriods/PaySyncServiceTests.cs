using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.PayPeriods;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.PayPeriods;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.PayPeriods;

public class PaySyncServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task SyncAsync_ThreeMonthlyPays_PicksUpThePay()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.TransactionScenario.WithTransaction("august", "main", Paid("ACME LTD", 2400m, 8, 28)))
            .Do(s => s.TransactionScenario.WithTransaction("september", "main", Paid("ACME LTD", 2400m, 9, 30)))
            .Do(s => s.TransactionScenario.WithTransaction("october", "main", Paid("ACME LTD", 2400m, 10, 30)));

        // Act
        bool found = await BuildSubject().SyncAsync(CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        PaySchedule schedule = await context.PaySchedules.SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(found).IsTrue();
            await Assert.That(schedule.Name).IsEqualTo("ACME LTD");
            await Assert.That(schedule.Rule).IsEqualTo(PayDayRule.LastWorkingDay);
            await Assert.That(schedule.AccountId).IsEqualTo(Scenario.AccountMappings["main"]);
            await Assert.That(schedule.PayDays).IsEquivalentTo(
                [new DateOnly(2026, 8, 28), new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 30)],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task SyncAsync_Again_KeepsOneSchedule()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.TransactionScenario.WithTransaction("august", "main", Paid("ACME LTD", 2400m, 8, 28)))
            .Do(s => s.TransactionScenario.WithTransaction("september", "main", Paid("ACME LTD", 2400m, 9, 30)))
            .Do(s => s.TransactionScenario.WithTransaction("october", "main", Paid("ACME LTD", 2400m, 10, 30)));
        PaySyncService subject = BuildSubject();
        await subject.SyncAsync(CancellationToken);

        // Act
        await subject.SyncAsync(CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        PaySchedule schedule = await context.PaySchedules.SingleAsync(CancellationToken);
        await Assert.That(schedule.PayDays).Count().IsEqualTo(3);
    }

    [Test]
    public async Task SyncAsync_NothingLikePay_AddsNothing()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.TransactionScenario.WithTransaction("refund", "main", Paid("SHOP REFUND", 20m, 10, 12)));

        // Act
        bool found = await BuildSubject().SyncAsync(CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(found).IsFalse();
            await Assert.That(await context.PaySchedules.AnyAsync(CancellationToken)).IsFalse();
        }
    }

    private Task<TestingScenario> SeedAsync() =>
        Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"));

    internal static TransactionDetails Paid(string description, decimal amount, int month, int day) =>
        new(Guid.NewGuid().ToString(), description, TransactionType.Credit, "CREDIT", amount, "GBP",
            new DateTime(2026, month, day, 9, 0, 0, DateTimeKind.Utc), false, [], TransactionMetadata.None, false);

    private PaySyncService BuildSubject() =>
        new(CurrentUserFor(Username), DbContextFactory, NullLogger<PaySyncService>.Instance,
            new FixedTimeProvider(Now));
}
