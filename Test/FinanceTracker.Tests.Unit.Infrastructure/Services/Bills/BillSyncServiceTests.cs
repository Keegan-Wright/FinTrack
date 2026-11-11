using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Bills;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Bills;

public class BillSyncServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task SyncAsync_NewStandingOrder_SeedsABillAndMatchesItsHistory()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.BankAccountScenario.WithStandingOrders("main", Rent(new DateTime(2026, 8, 15))))
            .Do(s => s.TransactionScenario.WithTransaction("august", "main", Paid("LANDLORD RENT", -650m, 8, 15)))
            .Do(s => s.TransactionScenario.WithTransaction("september", "main", Paid("LANDLORD RENT", -650m, 9, 15)))
            .Do(s => s.TransactionScenario.WithTransaction("october", "main", Paid("LANDLORD RENT", -650m, 10, 16)))
            .Do(s => s.TransactionScenario.WithTransaction("coffee", "main", Paid("COFFEE", -3.5m, 10, 15)));

        // Act
        BillSyncSummary summary = await BuildSubject().SyncAsync(CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Bill bill = await context.Bills.SingleAsync(CancellationToken);
        List<BankTransaction> paying = await context.BankTransactions
            .Where(x => x.BillPayment!.BillId == bill.Id)
            .ToListAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(summary.Added).IsEqualTo(1);
            await Assert.That(summary.Matched).IsEqualTo(3);
            await Assert.That(bill.Source).IsEqualTo(BillSource.StandingOrder);
            await Assert.That(paying.Select(static x => x.Id)).IsEquivalentTo([
                Scenario.TransactionMappings["august"], Scenario.TransactionMappings["september"],
                Scenario.TransactionMappings["october"]
            ]);
        }
    }

    [Test]
    public async Task SyncAsync_Again_SeedsAndMatchesNothingNew()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.BankAccountScenario.WithStandingOrders("main", Rent(new DateTime(2026, 9, 15))))
            .Do(s => s.TransactionScenario.WithTransaction("october", "main", Paid("LANDLORD RENT", -650m, 10, 15)));
        BillSyncService subject = BuildSubject();
        await subject.SyncAsync(CancellationToken);

        // Act
        BillSyncSummary summary = await subject.SyncAsync(CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(summary).IsEqualTo(new BillSyncSummary(0, 0, 0, 0, 0));
            await Assert.That(await context.Bills.CountAsync(CancellationToken)).IsEqualTo(1);
        }
    }

    [Test]
    public async Task SyncAsync_StandingOrderCancelled_EndsItsBill()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.BankAccountScenario.WithStandingOrders("main", Rent(new DateTime(2026, 9, 15))));
        BillSyncService subject = BuildSubject();
        await subject.SyncAsync(CancellationToken);
        await Scenario.Do(s => s.BankAccountScenario.WithStandingOrders("main"));

        // Act
        BillSyncSummary summary = await subject.SyncAsync(CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Bill bill = await context.Bills.SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(summary.Ended).IsEqualTo(1);
            await Assert.That(bill.HasEnded).IsTrue();
        }
    }

    [Test]
    public async Task SyncAsync_DirectDebitPriceRise_FollowsTheNewPrice()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.BankAccountScenario.WithDirectDebits("main", new DirectDebitDetails("dd-1", "Energy Co",
                "Active", new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc), -40m, "GBP", Now)))
            .Do(s => s.TransactionScenario.WithTransaction("september", "main", Paid("ENERGY CO DD", -40m, 9, 3)))
            .Do(s => s.TransactionScenario.WithTransaction("october", "main", Paid("ENERGY CO DD", -45m, 10, 3)));

        // Act
        BillSyncSummary summary = await BuildSubject().SyncAsync(CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Bill bill = await context.Bills.SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(summary.PricesChanged).IsEqualTo(1);
            await Assert.That(bill.ExpectedOn(new DateOnly(2026, 10, 3))).IsEqualTo(40m);
            await Assert.That(bill.ExpectedOn(new DateOnly(2026, 11, 3))).IsEqualTo(45m);
        }
    }

    [Test]
    public async Task SyncAsync_RepeatCardPayments_SuggestsASubscription()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.TransactionScenario.WithTransaction("august", "main", Paid("STREAMCO 111", -9.99m, 8, 12)))
            .Do(s => s.TransactionScenario.WithTransaction("september", "main", Paid("STREAMCO 222", -9.99m, 9, 12)))
            .Do(s => s.TransactionScenario.WithTransaction("october", "main", Paid("STREAMCO 333", -9.99m, 10, 12)));

        // Act
        BillSyncSummary summary = await BuildSubject().SyncAsync(CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        BillSuggestion suggestion = await context.BillSuggestions.SingleAsync(CancellationToken);
        string storedName = await context.Database
            .SqlQueryRaw<string>("SELECT \"Name\" AS \"Value\" FROM \"BillSuggestions\"")
            .SingleAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(summary.Suggested).IsEqualTo(1);
            await Assert.That(await context.Bills.AnyAsync(CancellationToken)).IsFalse();
            await Assert.That(suggestion.Name).IsEqualTo("STREAMCO");
            await Assert.That(suggestion.Frequency).IsEqualTo(BillFrequency.Monthly);
            await Assert.That(suggestion.Amount).IsEqualTo(9.99m);
            await Assert.That(suggestion.LastPaidOn).IsEqualTo(new DateOnly(2026, 10, 12));
            await Assert.That(suggestion.PaymentIds).Count().IsEqualTo(3);
            await Assert.That(storedName).DoesNotContain("STREAMCO");
        }
    }

    [Test]
    public async Task SyncAsync_DismissedSuggestion_IsntSuggestedAgain()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.TransactionScenario.WithTransaction("august", "main", Paid("STREAMCO", -9.99m, 8, 12)))
            .Do(s => s.TransactionScenario.WithTransaction("september", "main", Paid("STREAMCO", -9.99m, 9, 12)))
            .Do(s => s.TransactionScenario.WithTransaction("october", "main", Paid("STREAMCO", -9.99m, 10, 12)));
        BillSyncService subject = BuildSubject();
        await subject.SyncAsync(CancellationToken);
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            (await context.BillSuggestions.SingleAsync(CancellationToken)).Dismiss();
            await context.SaveChangesAsync(CancellationToken);
        }

        // Act
        BillSyncSummary summary = await subject.SyncAsync(CancellationToken);

        // Assert
        await using FinanceTrackerContext verification = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        BillSuggestion suggestion = await verification.BillSuggestions.SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(summary.Suggested).IsEqualTo(0);
            await Assert.That(suggestion.IsDismissed).IsTrue();
        }
    }

    [Test]
    public async Task SyncAsync_SuggestionNowABill_RemovesTheSuggestion()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.TransactionScenario.WithTransaction("august", "main", Paid("STREAMCO", -9.99m, 8, 12)))
            .Do(s => s.TransactionScenario.WithTransaction("september", "main", Paid("STREAMCO", -9.99m, 9, 12)))
            .Do(s => s.TransactionScenario.WithTransaction("october", "main", Paid("STREAMCO", -9.99m, 10, 12)));
        BillSyncService subject = BuildSubject();
        await subject.SyncAsync(CancellationToken);
        await Scenario.Do(s => s.BillScenario.WithBill("streaming", Username, "main", (userId, accountId) =>
            new Bill(userId, accountId, "Streaming", "Streamco", BillFrequency.Monthly, 9.99m,
                new DateOnly(2026, 8, 12), BillSource.Manual)));

        // Act
        BillSyncSummary summary = await subject.SyncAsync(CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(summary.Matched).IsEqualTo(3);
            await Assert.That(await context.BillSuggestions.AnyAsync(CancellationToken)).IsFalse();
        }
    }

    private Task<TestingScenario> SeedAsync() =>
        Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"));

    private static StandingOrderDetails Rent(DateTime firstPayment) =>
        new("RENT", "Landlord", "IntrvlMnthDay:01:15", "Active", "GBP",
            new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc), -650m, firstPayment, -650m, default, 0, Now);

    private static TransactionDetails Paid(string description, decimal amount, int month, int day) =>
        new(Guid.NewGuid().ToString(), description, TransactionType.Debit, "PURCHASE", amount, "GBP",
            new DateTime(2026, month, day, 12, 0, 0, DateTimeKind.Utc), false, [], TransactionMetadata.None, false);

    private BillSyncService BuildSubject() =>
        new(CurrentUserFor(Username), DbContextFactory, NullLogger<BillSyncService>.Instance,
            new FixedTimeProvider(Now), TestMetrics.Unobserved);
}
