using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Debts;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Debt = FinanceTracker.Contracts.Debts.Debt;
using DebtForecast = FinanceTracker.Contracts.Debts.DebtForecast;
using Transaction = FinanceTracker.Contracts.Transactions.Transaction;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Debts;

public class DebtsServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";
    private static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Started = new(2026, 9, 1);

    [Test]
    public async Task AddDebtAsync_ThenPayingItOff_OwesLessAndForecastsThePayOff()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.TransactionScenario.WithTransaction("october", "main", Paid("CAR FINANCE", -100m, 10, 1)));
        DebtsService subject = BuildSubject(Username);
        Debt added = await subject.AddDebtAsync(CarLoan(), CancellationToken);

        // Act
        Transaction paid = await subject.PayDebtAsync(Scenario.TransactionMappings["october"],
            new PayDebt { DebtId = added.Id }, CancellationToken);
        Debt debt = (await subject.GetDebtsAsync(CancellationToken).ToListAsync(CancellationToken)).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(added.Owed).IsEqualTo(1000m);
            await Assert.That(paid.Debt!.DebtName).IsEqualTo("Car loan");
            await Assert.That(paid.Debt.DecidedBy).IsEqualTo(TransferDecidedBy.User);
            await Assert.That(debt.Owed).IsEqualTo(900m);
            await Assert.That(debt.Paid).IsEqualTo(100m);
            await Assert.That(debt.PaidOffPercent).IsEqualTo(10m);
            await Assert.That(debt.Forecast.Months).IsEqualTo(9);
            await Assert.That(debt.OnTrack).IsTrue();
        }
    }

    [Test]
    public async Task GetDebtAsync_ListsItsPaymentsLatestFirstAndItsHistory()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.TransactionScenario.WithTransaction("september", "main", Paid("CAR FINANCE", -100m, 9, 1)))
            .Do(s => s.TransactionScenario.WithTransaction("october", "main", Paid("CAR FINANCE", -100m, 10, 1)));
        DebtsService subject = BuildSubject(Username);
        Debt added = await subject.AddDebtAsync(CarLoan(), CancellationToken);
        await subject.PayDebtAsync(Scenario.TransactionMappings["september"], new PayDebt { DebtId = added.Id },
            CancellationToken);
        await subject.PayDebtAsync(Scenario.TransactionMappings["october"], new PayDebt { DebtId = added.Id },
            CancellationToken);

        // Act
        DebtDetail detail = await subject.GetDebtAsync(added.Id, CancellationToken);

        // Assert: at the start, a month in and today
        using (Assert.Multiple())
        {
            await Assert.That(detail.Payments.Select(x => x.TransactionId)).IsEquivalentTo(
                [Scenario.TransactionMappings["october"], Scenario.TransactionMappings["september"]],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(detail.Payments[0].Amount).IsEqualTo(100m);
            await Assert.That(detail.Payments[0].AccountName).IsNotEmpty();
            await Assert.That(detail.History.Select(x => x.Owed)).IsEquivalentTo([1000m, 900m, 800m],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task UpdateDebtAsync_LinkedToACard_OwesItsBalanceWithHistoryFromItsDailyBalances()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.BankAccountScenario.WithBalance("main", new AccountBalance(0m, -450m, Now, 1500m)));
        Guid accountId = Scenario.AccountMappings["main"];
        await AddSnapshotsAsync(accountId, (new DateOnly(2026, 9, 20), -600m), (new DateOnly(2026, 9, 30), -550m),
            (new DateOnly(2026, 10, 29), -450m));
        DebtsService subject = BuildSubject(Username);
        Debt added = await subject.AddDebtAsync(CarLoan(), CancellationToken);

        // Act
        SaveDebt linked = CarLoan();
        linked.AccountId = accountId;
        Debt updated = await subject.UpdateDebtAsync(added.Id, linked, CancellationToken);
        DebtDetail detail = await subject.GetDebtAsync(added.Id, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(updated.Owed).IsEqualTo(450m);
            await Assert.That(updated.AccountId).IsEqualTo(accountId);
            await Assert.That(updated.AccountName).IsNotNull();
            await Assert.That(detail.History.Select(x => x.Owed)).IsEquivalentTo([550m, 450m],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task AddDebtAsync_NameOfOneTheyHadBefore_ThrowsAndAddsNothing()
    {
        // Arrange, the old one counts even once it's paid off, as its payments still name it
        await SeedAsync();
        DebtsService subject = BuildSubject(Username);
        await subject.AddDebtAsync(CarLoan(), CancellationToken);
        SaveDebt another = CarLoan();
        another.Name = " car LOAN";

        // Act
        NameTakenException? exception = await Assert.ThrowsAsync<NameTakenException>(() =>
            subject.AddDebtAsync(another, CancellationToken));

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).IsEqualTo("You already have a debt called car LOAN");
            await Assert.That(await context.Debts.CountAsync(CancellationToken)).IsEqualTo(1);
        }
    }

    [Test]
    public async Task UpdateDebtAsync_KeepingItsOwnName_Saves()
    {
        // Arrange
        await SeedAsync();
        DebtsService subject = BuildSubject(Username);
        Debt added = await subject.AddDebtAsync(CarLoan(), CancellationToken);
        SaveDebt edited = CarLoan();
        edited.MonthlyPayment = 150m;

        // Act
        Debt updated = await subject.UpdateDebtAsync(added.Id, edited, CancellationToken);

        // Assert
        await Assert.That(updated.MonthlyPayment).IsEqualTo(150m);
    }

    [Test]
    public async Task UpdateDebtAsync_ToAnotherDebtsName_Throws()
    {
        // Arrange
        await SeedAsync();
        DebtsService subject = BuildSubject(Username);
        await subject.AddDebtAsync(CarLoan(), CancellationToken);
        SaveDebt sofa = CarLoan();
        sofa.Name = "Sofa";
        Debt added = await subject.AddDebtAsync(sofa, CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<NameTakenException>(() =>
            subject.UpdateDebtAsync(added.Id, CarLoan(), CancellationToken));
    }

    [Test]
    public async Task AddDebtAsync_AnotherUsersAccount_Throws()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername))
            .Do(s => s.BankAccountScenario.WithAccount("their-card", "theirs"));
        SaveDebt debt = CarLoan();
        debt.AccountId = Scenario.AccountMappings["their-card"];

        // Act & Assert
        await Assert.That(async () => await BuildSubject(Username).AddDebtAsync(debt, CancellationToken))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetDebtAsync_AnotherUsersDebt_Throws()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.DebtScenario.WithDebt("theirs", OtherUsername));

        // Act & Assert
        await Assert.That(async () =>
                await BuildSubject(Username).GetDebtAsync(Scenario.DebtMappings["theirs"], CancellationToken))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task PayDebtAsync_ToNone_RecordsTheUsersAnswer()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.TransactionScenario.WithTransaction("october", "main", Paid("CAR FINANCE", -100m, 10, 1)));
        Guid transactionId = Scenario.TransactionMappings["october"];

        // Act
        Transaction paid = await BuildSubject(Username).PayDebtAsync(transactionId, new PayDebt(), CancellationToken);

        // Assert
        BankTransaction transaction = await LoadTransactionAsync(transactionId);
        using (Assert.Multiple())
        {
            await Assert.That(paid.Debt!.DebtId).IsNull();
            await Assert.That(transaction.DebtPayment!.DebtId).IsNull();
            await Assert.That(transaction.DebtPayment.Source).IsEqualTo(DecisionSource.User);
        }
    }

    [Test]
    public async Task PayDebtAsync_AnotherUsersDebt_Throws()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.TransactionScenario.WithTransaction("october", "main", Paid("CAR FINANCE", -100m, 10, 1)))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.DebtScenario.WithDebt("theirs", OtherUsername));

        // Act & Assert
        await Assert.That(async () => await BuildSubject(Username).PayDebtAsync(
                Scenario.TransactionMappings["october"], new PayDebt { DebtId = Scenario.DebtMappings["theirs"] },
                CancellationToken))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task DeleteDebtAsync_ItsPaymentsAndRulesNoLongerPayItOff()
    {
        // Arrange
        await SeedAsync()
            .Do(s => s.TransactionScenario.WithTransaction("october", "main", Paid("CAR FINANCE", -100m, 10, 1)));
        DebtsService subject = BuildSubject(Username);
        Debt added = await subject.AddDebtAsync(CarLoan(), CancellationToken);
        await subject.PayDebtAsync(Scenario.TransactionMappings["october"], new PayDebt { DebtId = added.Id },
            CancellationToken);
        await Scenario.Do(s => s.RuleScenario.WithRule("pays", Username, userId => new Rule(userId, "Car finance", 0,
            RuleMatchMode.All, [new RuleCondition(RuleField.Description, RuleOperator.Contains, "CAR FINANCE")],
            [new RuleAction(RuleActionType.PayDebt, null, null, null, added.Id)])));

        // Act
        await subject.DeleteDebtAsync(added.Id, CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        BankTransaction payment = await context.BankTransactions.SingleAsync(CancellationToken);
        Rule rule = await context.Rules.SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(await context.Debts.AnyAsync(CancellationToken)).IsFalse();
            await Assert.That(payment.DebtPayment).IsNull();
            await Assert.That(rule.Actions).IsEmpty();
            await Assert.That(rule.IsEnabled).IsFalse();
        }
    }

    [Test]
    public async Task ForecastAsync_PayingMore_PaysOffSooner()
    {
        // Arrange
        await SeedAsync();
        DebtsService subject = BuildSubject(Username);
        Debt added = await subject.AddDebtAsync(CarLoan(), CancellationToken);

        // Act
        DebtForecast more = await subject.ForecastAsync(added.Id, 250m, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(added.Forecast.Months).IsEqualTo(10);
            await Assert.That(more.Months).IsEqualTo(4);
        }
    }

    [Test]
    public async Task ForecastAsync_NoPayment_Throws()
    {
        // Arrange
        await SeedAsync().Do(s => s.DebtScenario.WithDebt("loan", Username));

        // Act & Assert
        await Assert.That(async () =>
                await BuildSubject(Username).ForecastAsync(Scenario.DebtMappings["loan"], 0m, CancellationToken))
            .Throws<ArgumentOutOfRangeException>();
    }

    private Task<TestingScenario> SeedAsync() =>
        Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"));

    private static SaveDebt CarLoan() => new()
    {
        Name = "Car loan",
        Lender = "Car Finance Co",
        StartingBalance = 1000m,
        StartedOn = Started,
        InterestRate = 0m,
        MonthlyPayment = 100m,
        PayOffGoal = new DateOnly(2027, 12, 1)
    };

    private static TransactionDetails Paid(string description, decimal amount, int month, int day) =>
        new(Guid.NewGuid().ToString(), description, TransactionType.Debit, "PURCHASE", amount, "GBP",
            new DateTime(2026, month, day, 12, 0, 0, DateTimeKind.Utc), false, [], TransactionMetadata.None, false);

    private async Task AddSnapshotsAsync(Guid accountId, params (DateOnly Date, decimal Current)[] balances)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid userId = Scenario.UserMappings[Username];
        foreach ((DateOnly date, decimal current) in balances)
        {
            context.BalanceSnapshots.Add(new BalanceSnapshot(userId, accountId, date,
                new AccountBalance(0m, current, Now, 1500m), BalanceSnapshotSource.Synced, isStale: false));
        }

        await context.SaveChangesAsync(CancellationToken);
    }

    private async Task<BankTransaction> LoadTransactionAsync(Guid id)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.BankTransactions.SingleAsync(x => x.Id == id, CancellationToken);
    }

    private DebtsService BuildSubject(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<DebtsService>.Instance, new FixedTimeProvider(Now));
}
