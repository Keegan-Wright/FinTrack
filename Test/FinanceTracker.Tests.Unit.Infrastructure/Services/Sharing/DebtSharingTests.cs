using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.NetWorth;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.NetWorth;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Debts;
using FinanceTracker.Infrastructure.NetWorth;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using DomainDebt = FinanceTracker.Domain.Debts.Debt;
using NotificationKind = FinanceTracker.Domain.Notifications.NotificationKind;
using Transaction = FinanceTracker.Contracts.Transactions.Transaction;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Sharing;

/// <summary>
/// Sam's debts seen by Alex. Credit card follows Card, which Sam shares with Alex. Family loan follows no account,
/// paid down by £100 from Sam's private Spending account. Car follows Car loan, an account Sam keeps private. Alex has
/// a coffee on their own account.
/// </summary>
public class DebtSharingTests : InfrastructureTestBase
{
    private const string Alex = "alex";
    private const string Sam = "sam";

    private static readonly DateTime Spent = DateTime.UtcNow.AddDays(-2);
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private ServiceProvider _services = null!;
    private Guid _linkId;
    private Guid _cardDebtId;
    private Guid _familyLoanId;
    private Guid _carId;

    [Before(Test)]
    public async Task SeedAsync()
    {
        List<(Guid UserId, NotificationKind Kind, string Title)> raised = [];
        ServiceCollection services = new();
        services.AddCurrentUser();
        services.AddScoped<INotifier>(sp => new RecordingNotifier(sp.GetRequiredService<ICurrentUser>(), raised));
        services.AddTransferMatching(DbContextFactory);
        _services = services.BuildServiceProvider();

        await Scenario.Do(s => s.UserScenario.WithUser("Alex", "Smith", "alex@example.test", Alex))
            .Do(s => s.UserScenario.WithUser("Sam", "Taylor", "sam@example.test", Sam))
            .Do(s => s.BankConnectionScenario.WithConnection("alex-bank", Alex))
            .Do(s => s.BankAccountScenario.WithAccount("alex-current", "alex-bank", Named("Alex current")))
            .Do(s => s.TransactionScenario.WithTransaction("alex-coffee", "alex-current", Details(-3m, "COFFEE")))
            .Do(s => s.BankConnectionScenario.WithConnection("sam-bank", Sam))
            .Do(s => s.BankAccountScenario.WithAccount("sam-card", "sam-bank", Named("Card")))
            .Do(s => s.BankAccountScenario.WithAccount("sam-spending", "sam-bank", Named("Spending")))
            .Do(s => s.BankAccountScenario.WithAccount("sam-car", "sam-bank", Named("Car loan")))
            .Do(s => s.BankAccountScenario.WithBalance("sam-card", new AccountBalance(0m, -500m, DateTime.UtcNow)))
            .Do(s => s.BankAccountScenario.WithBalance("sam-car", new AccountBalance(0m, -2_000m, DateTime.UtcNow)))
            .Do(s => s.TransactionScenario.WithTransaction("loan-payment", "sam-spending",
                Details(-100m, "MUM")));

        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            Guid samId = Scenario.UserMappings[Sam];
            DateOnly started = Today.AddMonths(-3);
            DomainDebt card = new(samId, "Credit card", "BANK", 600m, started, 0m, 50m);
            card.LinkAccount(await context.BankAccounts.SingleAsync(x => x.Id == Account("sam-card"),
                CancellationToken));
            DomainDebt family = new(samId, "Family loan", "Mum", 1_000m, started, 0m, 100m);
            DomainDebt car = new(samId, "Car", "LENDER", 3_000m, started, 0m, 200m);
            car.LinkAccount(await context.BankAccounts.SingleAsync(x => x.Id == Account("sam-car"),
                CancellationToken));
            context.Debts.AddRange(card, family, car);

            Guid paymentId = Transaction("loan-payment");
            BankTransaction payment = await context.BankTransactions.SingleAsync(x => x.Id == paymentId,
                CancellationToken);
            payment.PayDebt(family, samId, DateTime.UtcNow);
            await context.SaveChangesAsync(CancellationToken);

            _cardDebtId = card.Id;
            _familyLoanId = family.Id;
            _carId = car.Id;
        }

        InviteOutcome sent = await Sharing(Sam).InviteAsync(new InviteToShare { Who = Alex }, CancellationToken);
        await Sharing(Alex).AcceptAsync(sent.Link!.Id, CancellationToken);
        _linkId = sent.Link.Id;
        await ShareCardAsync(ShareMode.ReadOnly);
    }

    [After(Test)]
    public async Task TearDownAsync() => await _services.DisposeAsync();

    [Test]
    public async Task ADebtFollowingASharedAccount_GoesWithIt()
    {
        // Act
        List<Debt> alexs = await Debts(Alex).GetDebtsAsync(CancellationToken).ToListAsync(CancellationToken);
        LinkShares sams = (await Sharing(Sam).GetSharesAsync(_linkId, CancellationToken))!;

        // Assert
        Debt card = alexs.Single();
        using (Assert.Multiple())
        {
            await Assert.That((card.Name, card.SharedBy, card.CanChange, card.Owed, card.AccountName))
                .IsEqualTo(("Credit card", (string?)"Sam Taylor", false, 500m, (string?)"Card"));
            await Assert.That(sams.YourItems.Where(static x => x.Kind == SharedItemKind.Debt)
                    .Select(static x => (x.Name, x.FollowsAccount)))
                .IsEquivalentTo([("Car", (string?)null), ("Credit card", "Card"), ("Family loan", null)]);
        }
    }

    [Test]
    public async Task AChosenDebt_OwesTheSameForBoth_ButOnlyPaymentsAlexCanSeeAreListed()
    {
        // Arrange
        await ShareAsync(_familyLoanId);

        // Act
        DebtDetail asAlex = await Debts(Alex).GetDebtAsync(_familyLoanId, CancellationToken);
        DebtDetail asSam = await Debts(Sam).GetDebtAsync(_familyLoanId, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(asAlex.Debt.Owed).IsEqualTo(asSam.Debt.Owed);
            await Assert.That(asAlex.Debt.Paid).IsEqualTo(100m);
            await Assert.That(asAlex.Payments).IsEmpty();
            await Assert.That(asSam.Payments.Count).IsEqualTo(1);
        }
    }

    [Test]
    public async Task AChosenDebtFollowingAPrivateAccount_ShowsItsFigures_NotTheAccount()
    {
        // Arrange
        await ShareAsync(_carId);

        // Act
        Debt car = (await Debts(Alex).GetDebtAsync(_carId, CancellationToken)).Debt;

        // Assert
        await Assert.That((car.Owed, car.AccountName)).IsEqualTo((2_000m, (string?)"A private account"));
    }

    [Test]
    public async Task ReadOnly_AlexCantChangeIt()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Debts(Alex).UpdateDebtAsync(_cardDebtId, Save("Big card", Account("sam-card")), CancellationToken));
    }

    [Test]
    public async Task AccountSharedWithChanges_AlexEditsTheDebt_ButCantRelinkOrDeleteIt()
    {
        // Arrange
        await ShareCardAsync(ShareMode.AllowChanges);

        // Act
        Debt saved = await Debts(Alex).UpdateDebtAsync(_cardDebtId, Save("Big card", Account("sam-card")),
            CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((saved.Name, saved.SharedBy, saved.CanChange))
                .IsEqualTo(("Big card", (string?)"Sam Taylor", true));
            await Assert.ThrowsAsync<InvalidOperationException>(() => Debts(Alex)
                .UpdateDebtAsync(_cardDebtId, Save("Big card", null), CancellationToken));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Debts(Alex).DeleteDebtAsync(_cardDebtId, CancellationToken));
        }
    }

    [Test]
    public async Task AlexSaysTheirCoffeePaysOffSamsCard_ItShowsByName()
    {
        // Act
        Transaction paid = await Debts(Alex).PayDebtAsync(Transaction("alex-coffee"),
            new PayDebt { DebtId = _cardDebtId }, CancellationToken);

        // Assert
        await Assert.That(paid.Debt?.DebtName).IsEqualTo("Credit card");
    }

    [Test]
    public async Task UnsharingTheAccount_TakesItsDebtOffAlexsTransactions()
    {
        // Arrange
        await Debts(Alex).PayDebtAsync(Transaction("alex-coffee"), new PayDebt { DebtId = _cardDebtId },
            CancellationToken);

        // Act
        await Sharing(Sam).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares { Accounts = [] },
            CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(await Debts(Alex).GetDebtsAsync(CancellationToken).ToListAsync(CancellationToken))
                .IsEmpty();
            await Assert.That((await LoadAsync("alex-coffee")).DebtPayment).IsNull();
        }
    }

    [Test]
    public async Task NetWorth_ASharedDebtNotOnAnAccount_CountsInEveryone_NotJustMine()
    {
        // Arrange
        await ShareAsync(_familyLoanId);
        await AddNetWorthAsync(Alex, 1_000m);
        decimal owed = (await Debts(Sam).GetDebtAsync(_familyLoanId, CancellationToken)).Debt.Owed;

        // Act
        NetWorthHistory everyone = await NetWorth(Alex).GetNetWorthAsync(null, CancellationToken);
        await Sharing(Alex).ChangeViewAsync(new ChangeSharingView { IncludeShared = false }, CancellationToken);
        NetWorthHistory justMine = await NetWorth(Alex).GetNetWorthAsync(null, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(everyone.Breakdown.Select(static x => x.Name)).Contains("Family loan · Sam Taylor's");
            await Assert.That(everyone.Points[^1].Liabilities).IsEqualTo(owed);
            await Assert.That(justMine.Points[^1].Liabilities).IsEqualTo(0m);
        }
    }

    private Task<LinkShares> ShareCardAsync(ShareMode mode) =>
        Sharing(Sam).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares
        {
            Accounts = [new AccountShareChoice { AccountId = Account("sam-card"), Mode = mode }]
        }, CancellationToken);

    private Task<LinkShares> ShareAsync(Guid debtId) =>
        Sharing(Sam).ChangeItemSharesAsync(_linkId, new ChangeItemShares
        {
            Items = [new ItemShareChoice { Kind = SharedItemKind.Debt, ItemId = debtId, Mode = ShareMode.ReadOnly }]
        }, CancellationToken);

    private static SaveDebt Save(string name, Guid? accountId) => new()
    {
        Name = name,
        Lender = "BANK",
        StartingBalance = 600m,
        StartedOn = Today.AddMonths(-3),
        InterestRate = 0m,
        MonthlyPayment = 50m,
        AccountId = accountId
    };

    /// <summary>
    /// The user's own daily total for today, from no accounts but <paramref name="assets"/>.
    /// </summary>
    private async Task AddNetWorthAsync(string username, decimal assets)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid userId = Scenario.UserMappings[username];
        BankAccount account = await context.BankAccounts.FirstAsync(x => x.UserId == userId, CancellationToken);
        BalanceSnapshot balance = new(userId, account.Id, Today, new AccountBalance(assets, assets, DateTime.UtcNow),
            BalanceSnapshotSource.Synced, isStale: false);
        context.BalanceSnapshots.Add(balance);
        context.NetWorthSnapshots.Add(new NetWorthSnapshot(userId, Today, [balance]));
        await context.SaveChangesAsync(CancellationToken);
    }

    private async Task<BankTransaction> LoadAsync(string transaction)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid id = Transaction(transaction);
        return await context.BankTransactions.AsNoTracking().SingleAsync(x => x.Id == id, CancellationToken);
    }

    private static Func<Guid, Guid, BankAccount> Named(string name) => (userId, connectionId) =>
        new BankAccount(userId, connectionId, $"ext-{name}", "TRANSACTION", name, "GBP");

    private static TransactionDetails Details(decimal amount, string description) =>
        new TransactionDetailsFaker().Generate() with
        {
            Amount = amount,
            Description = description,
            Type = TransactionType.Debit,
            Currency = "GBP",
            OccurredAt = Spent,
            IsPending = false,
            LabelledAsTransfer = false,
            Metadata = TransactionMetadata.None
        };

    private Guid Account(string name) => Scenario.AccountMappings[name];

    private Guid Transaction(string name) => Scenario.TransactionMappings[name];

    private AccountScope Scope(string username) => new(CurrentUserFor(username));

    private DebtsService Debts(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<DebtsService>.Instance, TimeProvider.System,
            Scope(username));

    private NetWorthService NetWorth(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<NetWorthService>.Instance, TimeProvider.System,
            Scope(username));

    private SharingService Sharing(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<SharingService>.Instance,
            _services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
}
