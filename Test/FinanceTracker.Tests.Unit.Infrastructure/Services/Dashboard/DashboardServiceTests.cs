using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Domain.Bills;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Dashboard;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using BillState = FinanceTracker.Contracts.Bills.BillState;
using DomainBill = FinanceTracker.Domain.Bills.Bill;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Dashboard;

public class DashboardServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    private static readonly DateTime Now = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task GetSpentInTimePeriod_SplitsInOutTotals()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("salary", "main", Transaction(100m, Now, "CREDIT")))
            .Do(s => s.TransactionScenario.WithTransaction("shopping", "main", Transaction(-25m, Now, "PURCHASE")))
            .Do(s => s.TransactionScenario.WithTransaction("bill", "main", Transaction(-10m, Now, "BILL_PAYMENT")));

        DashboardService subject = BuildSubject();

        // Act
        SpentInTimePeriod result = await subject.GetSpentInTimePeriod(Now.AddDays(-1), Now.AddDays(1), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.TotalIn).IsEqualTo(100m);
            await Assert.That(result.TotalOut).IsEqualTo(-35m);
        }
    }

    [Test]
    public async Task GetSpentInTimePeriod_LeavesOutTransfersButCountsThoseUpForReview()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("salary", "main", Transaction(100m, Now, "CREDIT")))
            .Do(s => s.TransactionScenario.WithTransaction("to-savings", "main", Transaction(-500m, Now, "TRANSFER")))
            .Do(s => s.TransactionScenario.WithTransfer("to-savings"))
            .Do(s => s.TransactionScenario.WithTransaction("to-friend", "main", Transaction(-20m, Now, "TRANSFER")))
            .Do(s => s.TransactionScenario.WithTransfer("to-friend", TransferDecision.NeedsReview));

        DashboardService subject = BuildSubject();

        // Act
        SpentInTimePeriod result = await subject.GetSpentInTimePeriod(Now.AddDays(-1), Now.AddDays(1), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.TotalIn).IsEqualTo(100m);
            await Assert.That(result.TotalOut).IsEqualTo(-20m);
            await Assert.That(result.TransferredIn).IsEqualTo(0m);
            await Assert.That(result.TransferredOut).IsEqualTo(-500m);
        }
    }

    [Test]
    public async Task GetSpentInTimePeriod_IncludesTheStartAndLeavesOutTheEnd()
    {
        // Arrange
        DateTime from = new(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = new(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc);

        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("at-start", "main", Transaction(-1m, from)))
            .Do(s => s.TransactionScenario.WithTransaction("before-end", "main", Transaction(-2m, to.AddSeconds(-1))))
            .Do(s => s.TransactionScenario.WithTransaction("before-start", "main", Transaction(-4m, from.AddSeconds(-1))))
            .Do(s => s.TransactionScenario.WithTransaction("at-end", "main", Transaction(-8m, to)));

        DashboardService subject = BuildSubject();

        // Act
        SpentInTimePeriod result = await subject.GetSpentInTimePeriod(from, to, CancellationToken);

        // Assert
        await Assert.That(result.TotalOut).IsEqualTo(-3m);
    }

    [Test]
    public async Task GetSpentInTimePeriod_OnlyIncludesCurrentUsersTransactions()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("other-bank", OtherUsername))
            .Do(s => s.BankAccountScenario.WithAccount("other", "other-bank"))
            .Do(s => s.TransactionScenario.WithTransaction("theirs", "other", Transaction(-50m, Now)));

        DashboardService subject = BuildSubject();

        // Act
        SpentInTimePeriod result = await subject.GetSpentInTimePeriod(Now.AddDays(-1), Now.AddDays(1), CancellationToken);

        // Assert
        await Assert.That(result.TotalOut).IsEqualTo(0m);
    }

    [Test]
    public async Task GetSpentInTimePeriod_WhenUserHasNoTransactions_ReturnsZeroTotals()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        DashboardService subject = BuildSubject();

        // Act
        SpentInTimePeriod result = await subject.GetSpentInTimePeriod(Now.AddDays(-1), Now.AddDays(1), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.TotalIn).IsEqualTo(0m);
            await Assert.That(result.TotalOut).IsEqualTo(0m);
        }
    }

    [Test]
    public async Task GetUpcomingPaymentsAsync_BillsFromAWeekAgoSoonestFirst()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.BillScenario.WithBill("rent", Username, "main", (userId, accountId) =>
                Monthly(userId, accountId, "Rent", 650m, new DateOnly(2025, 6, 15), BillSource.StandingOrder,
                    Guid.NewGuid())))
            .Do(s => s.BillScenario.WithBill("streaming", Username, "main", (userId, accountId) =>
                Monthly(userId, accountId, "Streaming", 9.99m, new DateOnly(2025, 6, 9))))
            .Do(s => s.BillScenario.WithBill("gym", Username, "main", (userId, accountId) =>
                Monthly(userId, accountId, "Gym", 30m, new DateOnly(2025, 6, 5))))
            .Do(s => s.TransactionScenario.WithTransaction("streaming-paid", "main",
                Transaction(-10.20m, new DateTime(2026, 1, 9, 12, 0, 0, DateTimeKind.Utc))));
        await PayAsync("streaming-paid", "streaming");
        DashboardService subject = BuildSubject();

        // Act
        List<UpcomingPayment> payments = await subject.GetUpcomingPaymentsAsync(3, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(payments.Select(static x => x.PaymentName)).IsEquivalentTo(
                ["Gym", "Streaming", "Rent"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(payments[0].State).IsEqualTo(BillState.Late);
            await Assert.That(payments[0].Due).IsEqualTo(new DateOnly(2026, 1, 5));
            await Assert.That(payments[1].State).IsEqualTo(BillState.Paid);
            await Assert.That(payments[1].Amount).IsEqualTo(10.20m);
            await Assert.That(payments[2].State).IsEqualTo(BillState.Upcoming);
            await Assert.That(payments[2].Amount).IsEqualTo(650m);
            await Assert.That(payments[2].PaymentType).IsEqualTo("Standing order");
            await Assert.That(payments[2].BillId).IsEqualTo(Scenario.BillMappings["rent"]);
        }
    }

    [Test]
    public async Task GetUpcomingPaymentsAsync_EndedBill_LeavesOutWhatsNoLongerDue()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.BillScenario.WithBill("gym", Username, "main", (userId, accountId) =>
            {
                DomainBill bill = Monthly(userId, accountId, "Gym", 30m, new DateOnly(2025, 6, 20));
                bill.EndByUser(new DateOnly(2026, 1, 10));
                return bill;
            }));
        DashboardService subject = BuildSubject();

        // Act
        List<UpcomingPayment> payments = await subject.GetUpcomingPaymentsAsync(10, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(payments).IsEmpty();
    }

    [Test]
    public async Task GetUpcomingPaymentsAsync_OnlyIncludesCurrentUsersBills()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("other-bank", OtherUsername))
            .Do(s => s.BankAccountScenario.WithAccount("other", "other-bank"))
            .Do(s => s.BillScenario.WithBill("theirs", OtherUsername, "other", (userId, accountId) =>
                Monthly(userId, accountId, "Their rent", 900m, new DateOnly(2025, 6, 15))));
        DashboardService subject = BuildSubject();

        // Act
        List<UpcomingPayment> payments = await subject.GetUpcomingPaymentsAsync(10, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(payments).IsEmpty();
    }

    private static DomainBill Monthly(Guid userId, Guid accountId, string name, decimal amount, DateOnly startsOn,
        BillSource source = BillSource.Manual, Guid? sourceId = null) =>
        new(userId, accountId, name, name, BillFrequency.Monthly, amount, startsOn, source, sourceId);

    private async Task PayAsync(string transactionName, string billName)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid transactionId = Scenario.TransactionMappings[transactionName];
        Domain.OpenBanking.Transactions.BankTransaction transaction =
            await context.BankTransactions.SingleAsync(x => x.Id == transactionId, CancellationToken);
        transaction.DecideBillPayment(BillPayment.Matched(Scenario.BillMappings[billName], Now));
        await context.SaveChangesAsync(CancellationToken);
    }

    private Task<TestingScenario> SeedAccountAsync() =>
        Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"));

    private static Domain.OpenBanking.Transactions.TransactionDetails Transaction(decimal amount, DateTime occurredAt,
        string category = "PURCHASE") =>
        new TransactionDetailsFaker().Generate() with { Amount = amount, OccurredAt = occurredAt, Category = category };

    private DashboardService BuildSubject(DateTime? now = null) => new(CurrentUserFor(Username), DbContextFactory,
        NullLogger<DashboardService>.Instance, new FixedTimeProvider(now ?? Now));
}
