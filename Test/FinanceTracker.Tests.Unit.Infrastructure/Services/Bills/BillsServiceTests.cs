using FinanceTracker.Contracts.Bills;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Bills;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Bill = FinanceTracker.Contracts.Bills.Bill;
using BillFrequency = FinanceTracker.Contracts.Bills.BillFrequency;
using BillSource = FinanceTracker.Contracts.Bills.BillSource;
using BillState = FinanceTracker.Contracts.Bills.BillState;
using BillSuggestion = FinanceTracker.Contracts.Bills.BillSuggestion;
using DomainBill = FinanceTracker.Domain.Bills.Bill;
using DomainBillFrequency = FinanceTracker.Domain.Bills.BillFrequency;
using DomainBillSource = FinanceTracker.Domain.Bills.BillSource;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Bills;

public class BillsServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";
    private static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task AddBillAsync_MatchesItsHistoryAndShowsInTheMonth()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.TransactionScenario.WithTransaction("september", "main", Paid("STREAMCO 1", -9.99m, 9, 12)))
            .Do(s => s.TransactionScenario.WithTransaction("october", "main", Paid("STREAMCO 2", -9.99m, 10, 12)));
        BillsService subject = BuildSubject(Username);

        // Act
        Bill added = await subject.AddBillAsync(Streaming(new DateOnly(2026, 9, 12)), CancellationToken);
        BillMonthOverview october = await subject.GetMonthAsync(2026, 10, CancellationToken);

        // Assert
        BillStatus status = october.Bills.Single();
        using (Assert.Multiple())
        {
            await Assert.That(added.Source).IsEqualTo(BillSource.Manual);
            await Assert.That(added.AccountName).IsNotEmpty();
            await Assert.That(added.LastPaid).IsEqualTo(9.99m);
            await Assert.That(added.LastPaidOn).IsEqualTo(new DateOnly(2026, 10, 12));
            await Assert.That(added.NextDue).IsEqualTo(new DateOnly(2026, 11, 12));
            await Assert.That(added.CanDelete).IsTrue();
            await Assert.That(october.IsCurrent).IsTrue();
            await Assert.That(october.Committed).IsEqualTo(9.99m);
            await Assert.That(october.Due).IsEqualTo(9.99m);
            await Assert.That(october.Paid).IsEqualTo(9.99m);
            await Assert.That(status.Periods.Single().State).IsEqualTo(BillState.Paid);
            await Assert.That(status.Periods.Single().PaymentIds)
                .IsEquivalentTo([Scenario.TransactionMappings["october"]]);
        }
    }

    [Test]
    public async Task GetMonthAsync_BeforeABillStarted_LeavesItOut()
    {
        // Arrange
        await SeedAsync();
        BillsService subject = BuildSubject(Username);
        await subject.AddBillAsync(Streaming(new DateOnly(2026, 11, 12)), CancellationToken);

        // Act
        BillMonthOverview october = await subject.GetMonthAsync(2026, 10, CancellationToken);

        // Assert
        await Assert.That(october.Bills).IsEmpty();
    }

    [Test]
    public async Task AddBillAsync_AnotherUsersAccount_Throws()
    {
        // Arrange
        await SeedAsync();
        await Scenario.Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername))
            .Do(s => s.BankAccountScenario.WithAccount("theirs", "theirs"));
        SaveBill bill = Streaming(new DateOnly(2026, 11, 12));
        bill.AccountId = Scenario.AccountMappings["theirs"];

        // Act & Assert
        await Assert.That(async () => await BuildSubject(Username).AddBillAsync(bill, CancellationToken))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetBillAsync_AnotherUsersBill_Throws()
    {
        // Arrange
        await SeedAsync().Do(s => s.BillScenario.WithBill("streaming", Username, "main"));
        await Scenario.Do(s => s.UserScenario.WithUser(OtherUsername));

        // Act & Assert
        await Assert.That(async () =>
                await BuildSubject(OtherUsername).GetBillAsync(Scenario.BillMappings["streaming"], CancellationToken))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task UpdateBillAsync_NewAmount_AppliesFromTodayOn()
    {
        // Arrange
        await SeedAsync();
        BillsService subject = BuildSubject(Username);
        Bill added = await subject.AddBillAsync(Streaming(new DateOnly(2026, 9, 12)), CancellationToken);
        SaveBill changed = Streaming(new DateOnly(2026, 9, 12));
        changed.Name = "Films";
        changed.ExpectedAmount = 12.99m;

        // Act
        Bill updated = await subject.UpdateBillAsync(added.Id, changed, CancellationToken);

        // Assert
        DomainBill stored = await LoadBillAsync(added.Id);
        using (Assert.Multiple())
        {
            await Assert.That(updated.Name).IsEqualTo("Films");
            await Assert.That(updated.ExpectedAmount).IsEqualTo(12.99m);
            await Assert.That(stored.ExpectedOn(new DateOnly(2026, 10, 12))).IsEqualTo(9.99m);
            await Assert.That(stored.ExpectedOn(new DateOnly(2026, 11, 12))).IsEqualTo(12.99m);
        }
    }

    [Test]
    public async Task UpdateBillAsync_FromAStandingOrder_StaysOnItsAccount()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.BankAccountScenario.WithAccount("savings", "bank"))
            .Do(s => s.BillScenario.WithBill("rent", Username, "main", FromStandingOrder));
        Guid billId = Scenario.BillMappings["rent"];
        SaveBill changed = Streaming(new DateOnly(2026, 9, 15));
        changed.AccountId = Scenario.AccountMappings["savings"];

        // Act
        Bill updated = await BuildSubject(Username).UpdateBillAsync(billId, changed, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(updated.AccountId).IsEqualTo(Scenario.AccountMappings["main"]);
            await Assert.That(updated.FollowsTheBank).IsTrue();
            await Assert.That(updated.CanDelete).IsFalse();
        }
    }

    [Test]
    public async Task EndBillAsync_ThenReopen_IsDueAgain()
    {
        // Arrange
        await SeedAsync().Do(s => s.BillScenario.WithBill("streaming", Username, "main", static (userId, accountId) =>
            new DomainBill(userId, accountId, "Streaming", "Streamco", DomainBillFrequency.Monthly, 9.99m,
                new DateOnly(2026, 9, 12), DomainBillSource.Manual)));
        Guid billId = Scenario.BillMappings["streaming"];
        BillsService subject = BuildSubject(Username);

        // Act
        Bill ended = await subject.EndBillAsync(billId, CancellationToken);
        Bill reopened = await subject.ReopenBillAsync(billId, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ended.EndedOn).IsEqualTo(new DateOnly(2026, 10, 30));
            await Assert.That(ended.EndedByUser).IsTrue();
            await Assert.That(ended.NextDue).IsNull();
            await Assert.That(ended.MonthlyCost).IsEqualTo(0m);
            await Assert.That(reopened.EndedOn).IsNull();
            await Assert.That(reopened.NextDue).IsNotNull();
        }
    }

    [Test]
    public async Task GetMonthAsync_EndedAfterItWasDue_StillShowsInThatMonthOnly()
    {
        // Arrange, due on the 12th and ended on 30 Oct
        await SeedAsync().Do(s => s.BillScenario.WithBill("streaming", Username, "main", static (userId, accountId) =>
            new DomainBill(userId, accountId, "Streaming", "Streamco", DomainBillFrequency.Monthly, 9.99m,
                new DateOnly(2026, 9, 12), DomainBillSource.Manual)));
        BillsService subject = BuildSubject(Username);
        await subject.EndBillAsync(Scenario.BillMappings["streaming"], CancellationToken);

        // Act
        BillMonthOverview october = await subject.GetMonthAsync(2026, 10, CancellationToken);
        BillMonthOverview november = await subject.GetMonthAsync(2026, 11, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(october.Bills.Single().Periods.Single().Due).IsEqualTo(new DateOnly(2026, 10, 12));
            await Assert.That(october.Due).IsEqualTo(9.99m);
            await Assert.That(november.Bills).IsEmpty();
        }
    }

    [Test]
    public async Task GetMonthAsync_EndedBeforeItWasDue_LeavesItOutOfTheMonth()
    {
        // Arrange, first due on 31 Oct and ended on 30 Oct, so it was never due
        await SeedAsync().Do(s => s.BillScenario.WithBill("streaming", Username, "main", static (userId, accountId) =>
            new DomainBill(userId, accountId, "Streaming", "Streamco", DomainBillFrequency.Monthly, 9.99m,
                new DateOnly(2026, 10, 31), DomainBillSource.Manual)));
        BillsService subject = BuildSubject(Username);
        await subject.EndBillAsync(Scenario.BillMappings["streaming"], CancellationToken);

        // Act
        BillMonthOverview october = await subject.GetMonthAsync(2026, 10, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(october.Bills).IsEmpty();
            await Assert.That(october.Committed).IsEqualTo(0m);
        }
    }

    [Test]
    public async Task GetMonthAsync_Quarterly_IsDueOnlyInItsMonthsButListedInEach()
    {
        // Arrange, due 12 Sep, 12 Dec, ...
        await SeedAsync().Do(s => s.BillScenario.WithBill("insurance", Username, "main", static (userId, accountId) =>
            new DomainBill(userId, accountId, "Insurance", "Insurer", DomainBillFrequency.Quarterly, 90m,
                new DateOnly(2026, 9, 12), DomainBillSource.Manual)));
        BillsService subject = BuildSubject(Username);

        // Act
        BillMonthOverview october = await subject.GetMonthAsync(2026, 10, CancellationToken);
        BillMonthOverview december = await subject.GetMonthAsync(2026, 12, CancellationToken);

        // Assert, still running in October so listed, not due, a third of it committed a month
        using (Assert.Multiple())
        {
            await Assert.That(october.Bills.Single().Periods).IsEmpty();
            await Assert.That(october.Due).IsEqualTo(0m);
            await Assert.That(october.Committed).IsEqualTo(30m);
            await Assert.That(december.Bills.Single().Periods.Single().Due).IsEqualTo(new DateOnly(2026, 12, 12));
            await Assert.That(december.Due).IsEqualTo(90m);
        }
    }

    [Test]
    public async Task GetMonthAsync_EndedOnItsDueDay_IsStillDueThatDay()
    {
        // Arrange, its last day is the day it's due
        await SeedAsync().Do(s => s.BillScenario.WithBill("streaming", Username, "main", static (userId, accountId) =>
        {
            DomainBill bill = new(userId, accountId, "Streaming", "Streamco", DomainBillFrequency.Monthly, 9.99m,
                new DateOnly(2026, 9, 12), DomainBillSource.Manual);
            bill.End(new DateOnly(2026, 10, 12));
            return bill;
        }));
        BillsService subject = BuildSubject(Username);

        // Act
        BillMonthOverview october = await subject.GetMonthAsync(2026, 10, CancellationToken);
        BillMonthOverview november = await subject.GetMonthAsync(2026, 11, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(october.Bills.Single().Periods.Single().Due).IsEqualTo(new DateOnly(2026, 10, 12));
            await Assert.That(november.Bills).IsEmpty();
        }
    }

    [Test]
    public async Task DeleteBillAsync_ItsPaymentsAndRulesNoLongerPayIt()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.TransactionScenario.WithTransaction("october", "main", Paid("STREAMCO", -9.99m, 10, 12)));
        BillsService subject = BuildSubject(Username);
        Bill added = await subject.AddBillAsync(Streaming(new DateOnly(2026, 9, 12)), CancellationToken);
        await Scenario.Do(s => s.RuleScenario.WithRule("pays", Username, userId => new Rule(userId, "Streaming", 0,
            RuleMatchMode.All, [new RuleCondition(RuleField.Description, RuleOperator.Contains, "STREAMCO")],
            [new RuleAction(RuleActionType.PayBill, null, null, added.Id)])));

        // Act
        await subject.DeleteBillAsync(added.Id, CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        BankTransaction payment = await context.BankTransactions.SingleAsync(CancellationToken);
        Rule rule = await context.Rules.SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(await context.Bills.AnyAsync(CancellationToken)).IsFalse();
            await Assert.That(payment.BillPayment).IsNull();
            await Assert.That(rule.Actions).IsEmpty();
            await Assert.That(rule.IsEnabled).IsFalse();
        }
    }

    [Test]
    public async Task DeleteBillAsync_FromAStandingOrder_CanOnlyBeEnded()
    {
        // Arrange
        await SeedAsync().Do(s => s.BillScenario.WithBill("rent", Username, "main", FromStandingOrder));

        // Act & Assert
        await Assert.That(async () =>
                await BuildSubject(Username).DeleteBillAsync(Scenario.BillMappings["rent"], CancellationToken))
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task PayBillAsync_ToABillThenNone_RecordsTheUsersAnswer()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.BillScenario.WithBill("streaming", Username, "main"))
            .Do(s => s.TransactionScenario.WithTransaction("gift", "main", Paid("GIFT CARD", -9.99m, 10, 12)));
        Guid transactionId = Scenario.TransactionMappings["gift"];
        Guid billId = Scenario.BillMappings["streaming"];
        BillsService subject = BuildSubject(Username);

        // Act
        await subject.PayBillAsync(transactionId, new PayBill { BillId = billId }, CancellationToken);
        bool paidTheBill = (await LoadTransactionAsync(transactionId)).PaysBill(billId);
        await subject.PayBillAsync(transactionId, new PayBill(), CancellationToken);

        // Assert
        BankTransaction transaction = await LoadTransactionAsync(transactionId);
        using (Assert.Multiple())
        {
            await Assert.That(paidTheBill).IsTrue();
            await Assert.That(transaction.BillPayment!.BillId).IsNull();
            await Assert.That(transaction.BillPayment.Source).IsEqualTo(Domain.Common.DecisionSource.User);
        }
    }

    [Test]
    public async Task PayBillAsync_AnotherUsersBill_Throws()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.TransactionScenario.WithTransaction("gift", "main", Paid("GIFT CARD", -9.99m, 10, 12)));
        await Scenario.Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername))
            .Do(s => s.BankAccountScenario.WithAccount("theirs", "theirs"))
            .Do(s => s.BillScenario.WithBill("theirs", OtherUsername, "theirs"));

        // Act & Assert
        await Assert.That(async () => await BuildSubject(Username).PayBillAsync(Scenario.TransactionMappings["gift"],
                new PayBill { BillId = Scenario.BillMappings["theirs"] }, CancellationToken))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetSuggestionsAsync_WaitingOnesWithTheirPayments()
    {
        // Arrange
        await SeedSuggestionAsync();
        BillsService subject = BuildSubject(Username);

        // Act
        List<BillSuggestion> suggestions =
            await subject.GetSuggestionsAsync(CancellationToken).ToListAsync(CancellationToken);
        int count = await subject.GetSuggestionCountAsync(CancellationToken);

        // Assert
        BillSuggestion suggestion = suggestions.Single();
        using (Assert.Multiple())
        {
            await Assert.That(count).IsEqualTo(1);
            await Assert.That(suggestion.Name).IsEqualTo("Streamco");
            await Assert.That(suggestion.Frequency).IsEqualTo(BillFrequency.Monthly);
            await Assert.That(suggestion.NextDue).IsEqualTo(new DateOnly(2026, 11, 12));
            await Assert.That(suggestion.Payments.Select(static x => x.Description))
                .IsEquivalentTo(["STREAMCO 1", "STREAMCO 2", "STREAMCO 3"]);
        }
    }

    [Test]
    public async Task ConfirmSuggestionAsync_MakesABillPaidByItsPayments()
    {
        // Arrange
        await SeedSuggestionAsync();
        BillsService subject = BuildSubject(Username);

        // Act
        Bill bill = await subject.ConfirmSuggestionAsync(Scenario.BillSuggestionMappings["streaming"],
            CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        int paying = await context.BankTransactions.CountAsync(x => x.BillPayment!.BillId == bill.Id,
            CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(bill.Source).IsEqualTo(BillSource.Detected);
            await Assert.That(bill.Name).IsEqualTo("Streamco");
            await Assert.That(bill.LastPaidOn).IsEqualTo(new DateOnly(2026, 10, 12));
            await Assert.That(bill.CanDelete).IsTrue();
            await Assert.That(paying).IsEqualTo(3);
            await Assert.That(await context.BillSuggestions.AnyAsync(CancellationToken)).IsFalse();
        }
    }

    [Test]
    public async Task ConfirmSuggestionAsync_ABillAddedSinceCoversIt_ThrowsAndDropsTheSuggestion()
    {
        // Arrange
        await SeedSuggestionAsync();
        BillsService subject = BuildSubject(Username);
        await subject.AddBillAsync(Streaming(new DateOnly(2026, 11, 12)), CancellationToken);

        // Act
        AlreadyABillException? exception = await Assert.ThrowsAsync<AlreadyABillException>(() =>
            subject.ConfirmSuggestionAsync(Scenario.BillSuggestionMappings["streaming"], CancellationToken));

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).IsEqualTo("Your bill Streaming already covers these payments");
            await Assert.That(await context.Bills.CountAsync(CancellationToken)).IsEqualTo(1);
            await Assert.That(await context.BillSuggestions.AnyAsync(CancellationToken)).IsFalse();
        }
    }

    [Test]
    public async Task ConfirmSuggestionAsync_AnEndedBillForThePayee_StillMakesABill()
    {
        // Arrange
        await SeedSuggestionAsync();
        BillsService subject = BuildSubject(Username);
        Bill old = await subject.AddBillAsync(Streaming(new DateOnly(2026, 11, 12)), CancellationToken);
        await subject.EndBillAsync(old.Id, CancellationToken);

        // Act
        Bill bill = await subject.ConfirmSuggestionAsync(Scenario.BillSuggestionMappings["streaming"],
            CancellationToken);

        // Assert
        await Assert.That(bill.Name).IsEqualTo("Streamco");
    }

    [Test]
    public async Task DismissSuggestionAsync_IsNoLongerWaiting()
    {
        // Arrange
        await SeedSuggestionAsync();
        BillsService subject = BuildSubject(Username);
        Guid id = Scenario.BillSuggestionMappings["streaming"];

        // Act
        await subject.DismissSuggestionAsync(id, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(await subject.GetSuggestionCountAsync(CancellationToken)).IsEqualTo(0);
            await Assert.That(async () => await subject.ConfirmSuggestionAsync(id, CancellationToken))
                .Throws<InvalidOperationException>();
        }
    }

    [Test]
    public async Task GetMonthAsync_PaidOnTheLastWorkingDay_CoversFromPayToPay()
    {
        // Arrange: paid Friday 30 Oct, so November runs to the day before the next pay on Monday 30 Nov
        await SeedAsync()
            .Do(s => s.PayScenario.WithPayOnTheLastWorkingDay(Username, "main", new DateOnly(2026, 8, 28),
                new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 30)));
        BillsService subject = BuildSubject(Username);
        await subject.AddBillAsync(Streaming(new DateOnly(2026, 11, 12)), CancellationToken);

        // Act
        BillMonthOverview november = await subject.GetMonthAsync(2026, 11, CancellationToken);
        BillMonthOverview october = await subject.GetMonthAsync(2026, 10, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(november.Month).IsEqualTo(new DateOnly(2026, 11, 1));
            await Assert.That(november.Start).IsEqualTo(new DateOnly(2026, 10, 30));
            await Assert.That(november.End).IsEqualTo(new DateOnly(2026, 11, 29));
            await Assert.That(november.IsCurrent).IsTrue();
            await Assert.That(november.Due).IsEqualTo(9.99m);
            await Assert.That(october.IsCurrent).IsFalse();
            await Assert.That(october.End).IsEqualTo(new DateOnly(2026, 10, 29));
        }
    }

    private Task<TestingScenario> SeedAsync() =>
        Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"));

    private Task<TestingScenario> SeedSuggestionAsync() =>
        SeedAsync()
            .Do(s => s.TransactionScenario.WithTransaction("august", "main", Paid("STREAMCO 1", -9.99m, 8, 12)))
            .Do(s => s.TransactionScenario.WithTransaction("september", "main", Paid("STREAMCO 2", -9.99m, 9, 12)))
            .Do(s => s.TransactionScenario.WithTransaction("october", "main", Paid("STREAMCO 3", -9.99m, 10, 12)))
            .Do(s => s.BillScenario.WithSuggestion("streaming", Username, "main", "Streamco", 9.99m,
                new DateOnly(2026, 8, 12), new DateOnly(2026, 10, 12), "august", "september", "october"));

    private SaveBill Streaming(DateOnly nextDue) => new()
    {
        Name = "Streaming",
        Payee = "Streamco",
        AccountId = Scenario.AccountMappings["main"],
        Frequency = BillFrequency.Monthly,
        NextDue = nextDue,
        ExpectedAmount = 9.99m,
        TolerancePercent = 5
    };

    private static DomainBill FromStandingOrder(Guid userId, Guid accountId) =>
        new(userId, accountId, "Rent", "Landlord", DomainBillFrequency.Monthly, 650m, new DateOnly(2026, 9, 15),
            DomainBillSource.StandingOrder, Guid.NewGuid());

    private static TransactionDetails Paid(string description, decimal amount, int month, int day) =>
        new(Guid.NewGuid().ToString(), description, TransactionType.Debit, "PURCHASE", amount, "GBP",
            new DateTime(2026, month, day, 12, 0, 0, DateTimeKind.Utc), false, [], TransactionMetadata.None, false);

    private async Task<DomainBill> LoadBillAsync(Guid id)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.Bills.SingleAsync(x => x.Id == id, CancellationToken);
    }

    private async Task<BankTransaction> LoadTransactionAsync(Guid id)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.BankTransactions.SingleAsync(x => x.Id == id, CancellationToken);
    }

    private BillsService BuildSubject(string username)
    {
        FixedTimeProvider time = new(Now);
        return new BillsService(CurrentUserFor(username), DbContextFactory, NullLogger<BillsService>.Instance,
            new BillSyncService(CurrentUserFor(username), DbContextFactory, NullLogger<BillSyncService>.Instance,
                time, TestMetrics.Unobserved), time, new AccountScope(CurrentUserFor(username)));
    }
}
