using FinanceTracker.Contracts.Account;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.NetWorth;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.NetWorth;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account;
using FinanceTracker.Infrastructure.Classification;
using FinanceTracker.Infrastructure.NetWorth;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Infrastructure.Transactions;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationKind = FinanceTracker.Domain.Notifications.NotificationKind;
using Transaction = FinanceTracker.Contracts.Transactions.Transaction;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Sharing;

/// <summary>
/// A joint account Sam and Alex have both connected and merged, Sam's copy shown. Sam's consent lapsed five days ago,
/// so Sam's copy stops at the shop ten days ago while Alex's carries on with a coffee and the rent.
/// </summary>
public class JointAccountGapsTests : InfrastructureTestBase
{
    private const string Alex = "alex";
    private const string Sam = "sam";

    private static readonly DateTime Shop = DateTime.UtcNow.AddDays(-10);
    private static readonly DateTime Coffee = DateTime.UtcNow.AddDays(-4);
    private static readonly DateTime Rent = DateTime.UtcNow.AddDays(-2);
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly List<(Guid UserId, NotificationKind Kind, string Title)> _raised = [];
    private ServiceProvider _services = null!;
    private Guid _linkId;

    [Before(Test)]
    public async Task SeedAsync()
    {
        ServiceCollection services = new();
        services.AddCurrentUser();
        services.AddScoped<INotifier>(sp => new RecordingNotifier(sp.GetRequiredService<ICurrentUser>(), _raised));
        services.AddTransferMatching(DbContextFactory);
        _services = services.BuildServiceProvider();

        await Scenario.Do(s => s.UserScenario.WithUser("Alex", "Smith", "alex@example.test", Alex))
            .Do(s => s.UserScenario.WithUser("Sam", "Taylor", "sam@example.test", Sam))
            .Do(s => s.BankConnectionScenario.WithConnection("sam-bank", Sam))
            .Do(s => s.BankAccountScenario.WithAccount("sam-joint", "sam-bank", Joint("Joint")))
            .Do(s => s.TransactionScenario.WithTransaction("shop-sam", "sam-joint", Details(Shop, -40m, "TESCO")))
            .Do(s => s.BankConnectionScenario.WithConnection("alex-bank", Alex))
            .Do(s => s.BankAccountScenario.WithAccount("alex-current", "alex-bank", Named("Alex current")))
            .Do(s => s.BankAccountScenario.WithAccount("alex-joint", "alex-bank", Joint("Joint account")))
            .Do(s => s.TransactionScenario.WithTransaction("shop-alex", "alex-joint", Details(Shop, -40m, "TESCO")))
            .Do(s => s.TransactionScenario.WithTransaction("coffee-alex", "alex-joint",
                Details(Coffee, -3m, "COFFEE")))
            .Do(s => s.TransactionScenario.WithTransaction("rent-alex", "alex-joint", Details(Rent, -900m, "RENT")))
            .Do(s => s.BankAccountScenario.WithBalance("sam-joint", new AccountBalance(1_000m, 1_000m, Shop)))
            .Do(s => s.BankAccountScenario.WithBalance("alex-joint",
                new AccountBalance(97m, 97m, DateTime.UtcNow)))
            .Do(s => s.ClassificationScenario.WithClassification("groceries", Sam, "Groceries"));

        InviteOutcome sent = await Sharing(Sam).InviteAsync(new InviteToShare { Who = Alex }, CancellationToken);
        await Sharing(Alex).AcceptAsync(sent.Link!.Id, CancellationToken);
        _linkId = sent.Link.Id;

        await Sharing(Sam).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares
        {
            Accounts = [new AccountShareChoice { AccountId = Account("sam-joint"), Mode = Contracts.Sharing.ShareMode.ReadOnly }]
        }, CancellationToken);
        await Sharing(Alex).MergeJointAccountsAsync(_linkId, new MergeJointAccounts
        {
            Accounts =
            [
                new MergeJointAccount { YourAccountId = Account("alex-joint"), TheirAccountId = Account("sam-joint") }
            ]
        }, CancellationToken);

        await Scenario.Do(s => s.BankConnectionScenario.WithConsentExpiresAt("sam-bank", DateTime.UtcNow.AddDays(-5)));
    }

    [After(Test)]
    public async Task TearDownAsync() => await _services.DisposeAsync();

    [Test]
    public async Task ShownCopyLapsed_IsFilledFromTheOtherCopy_ForBoth()
    {
        // Act
        await RefreshAsync(Alex);

        // Assert
        List<Transaction> asSam = await SeenBy(Sam);
        List<Transaction> asAlex = await SeenBy(Alex);
        AccountAndTransactions samsCard = (await Accounts(Sam)).Single();
        using (Assert.Multiple())
        {
            await Assert.That(asSam.Select(static x => (x.Description, x.AccountName)))
                .IsEquivalentTo([("TESCO", "Joint"), ("COFFEE", "Joint"), ("RENT", "Joint")]);
            await Assert.That(asSam.Select(static x => x.AccountId)).All().Satisfy(x => x.IsEqualTo(Account("sam-joint")));
            await Assert.That(asAlex.Select(static x => x.Description)).IsEquivalentTo(["TESCO", "COFFEE", "RENT"]);
            await Assert.That(samsCard.AccountBalance).IsEqualTo(97m);
            await Assert.That(samsCard.Transactions!.Select(static x => x.Description))
                .IsEquivalentTo(["TESCO", "COFFEE", "RENT"]);
        }
    }

    [Test]
    public async Task FilledTransaction_CanBeTaggedByEither()
    {
        // Arrange
        await RefreshAsync(Alex);

        // Act
        await TagAsSamAsync("coffee-alex", "groceries");

        // Assert
        Transaction asAlex = (await SeenBy(Alex)).Single(x => x.Description == "COFFEE");
        await Assert.That(asAlex.CustomTags).IsEquivalentTo(["Groceries"]);
    }

    [Test]
    public async Task RenewedWithItsWholeHistory_TheShownCopyTakesOver_WithTheTagsPutOnMeanwhile()
    {
        // Arrange, tagged while Alex's copy filled in
        await RefreshAsync(Alex);
        await TagAsSamAsync("coffee-alex", "groceries");

        // Act, Sam renews and the bank gives the whole stretch back
        await RenewSamAsync(("coffee-sam", Coffee, -3m, "COFFEE"), ("rent-sam", Rent, -900m, "RENT"));
        await RefreshAsync(Sam);

        // Assert
        List<Transaction> asSam = await SeenBy(Sam);
        JointAccount joint = await JointAsync();
        using (Assert.Multiple())
        {
            await Assert.That(asSam.Select(static x => x.TransactionId))
                .IsEquivalentTo([Transaction("shop-sam"), Transaction("coffee-sam"), Transaction("rent-sam")]);
            await Assert.That(asSam.Single(x => x.Description == "COFFEE").CustomTags).IsEquivalentTo(["Groceries"]);
            await Assert.That(joint.HasOpenGap).IsFalse();
            await Assert.That((await Accounts(Sam)).Single().AccountBalance).IsEqualTo(1_000m);
        }
    }

    [Test]
    public async Task RenewedWithLessHistory_WhatTheBankWontGiveBackStaysFilled()
    {
        // Arrange
        await RefreshAsync(Alex);

        // Act, the bank only gives back the rent
        await RenewSamAsync(("rent-sam", Rent, -900m, "RENT"));
        await RefreshAsync(Sam);

        // Assert
        List<Transaction> asSam = await SeenBy(Sam);
        await Assert.That(asSam.Select(static x => x.TransactionId))
            .IsEquivalentTo([Transaction("shop-sam"), Transaction("coffee-alex"), Transaction("rent-sam")]);
    }

    [Test]
    public async Task NotLapsed_NothingFills()
    {
        // Arrange
        await Scenario.Do(s => s.BankConnectionScenario.WithConsentExpiresAt("sam-bank", DateTime.UtcNow.AddDays(30)));

        // Act
        await RefreshAsync(Alex);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((await SeenBy(Sam)).Select(static x => x.Description)).IsEquivalentTo(["TESCO"]);
            await Assert.That((await JointAsync()).Gaps).IsEmpty();
        }
    }

    [Test]
    public async Task Splitting_StopsFilling()
    {
        // Arrange
        await RefreshAsync(Alex);
        LinkShares shares = (await Sharing(Alex).GetSharesAsync(_linkId, CancellationToken))!;

        // Act
        await Sharing(Alex).SplitJointAccountAsync(_linkId, shares.JointAccounts.Single().JointAccountId,
            CancellationToken);

        // Assert, Sam has their own copy alone again
        using (Assert.Multiple())
        {
            await Assert.That((await SeenBy(Sam)).Select(static x => x.Description)).IsEquivalentTo(["TESCO"]);
            await Assert.That((await LoadAsync("coffee-alex")).FillsJointGap).IsFalse();
        }
    }

    [Test]
    public async Task MoneyMovedIntoTheJointAccountMeanwhile_PairsWithTheFilledTransaction()
    {
        // Arrange, Alex pays in from their current account while Sam's copy has lapsed
        await Scenario
            .Do(s => s.TransactionScenario.WithTransaction("out-alex", "alex-current",
                Details(Rent.AddHours(-1), -50m, "TO JOINT", labelled: true)))
            .Do(s => s.TransactionScenario.WithTransaction("in-alex", "alex-joint",
                Details(Rent.AddHours(-1), 50m, "FROM ALEX", labelled: true)));

        // Act
        await RefreshAsync(Alex);

        // Assert
        Transaction paidIn = (await SeenBy(Alex)).Single(x => x.Description == "TO JOINT");
        using (Assert.Multiple())
        {
            await Assert.That(paidIn.Transfer?.PartnerTransactionId).IsEqualTo(Transaction("in-alex"));
            await Assert.That(paidIn.Transfer?.PartnerAccountId).IsEqualTo(Account("sam-joint"));
            await Assert.That(paidIn.Transfer?.PartnerAccountName).IsEqualTo("Joint");
        }
    }

    [Test]
    public async Task NetWorth_TakesTheJointAccountsBalanceFromTheOtherCopy_ForBoth()
    {
        // Arrange, today's balances: Sam's copy stuck at 1,000, Alex's up to date at 97
        await AddBalanceAsync("sam-joint", 1_000m);
        await AddBalanceAsync("alex-current", 500m);
        await AddBalanceAsync("alex-joint", 97m);

        // Act
        await RefreshAsync(Alex);

        // Assert
        NetWorthHistory sams = await NetWorth(Sam).GetNetWorthAsync(null, CancellationToken);
        NetWorthHistory alexs = await NetWorth(Alex).GetNetWorthAsync(null, CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(sams.Points[^1].NetWorth).IsEqualTo(97m);
            await Assert.That(alexs.Points[^1].NetWorth).IsEqualTo(597m);
            await Assert.That(sams.Breakdown.Single().Amount).IsEqualTo(97m);
        }
    }

    /// <summary>
    /// Sam renews, and the sync brings in <paramref name="transactions"/> on Sam's copy.
    /// </summary>
    private async Task RenewSamAsync(params (string Name, DateTime At, decimal Amount, string Description)[] transactions)
    {
        await Scenario.Do(s => s.BankConnectionScenario.WithConsentExpiresAt("sam-bank", DateTime.UtcNow.AddDays(90)));
        foreach ((string name, DateTime at, decimal amount, string description) in transactions)
        {
            await Scenario.Do(s => s.TransactionScenario.WithTransaction(name, "sam-joint",
                Details(at, amount, description)));
        }

        await Scenario.Do(s => s.BankAccountScenario.WithSyncLog("sam-joint", SyncType.Transactions,
            DateTime.UtcNow.AddMinutes(1)));
    }

    private async Task RefreshAsync(string username)
    {
        await using AsyncServiceScope scope = _services.GetRequiredService<IServiceScopeFactory>()
            .CreateUserScope(Scenario.UserMappings[username]);
        JointAccountGapService gaps = new(CurrentUserFor(username), DbContextFactory,
            NullLogger<JointAccountGapService>.Instance, TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<ITransferMatchingService>());
        await gaps.RefreshAsync(CancellationToken);
    }

    private async Task<JointAccount> JointAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.JointAccounts.AsNoTracking().SingleAsync(CancellationToken);
    }

    /// <summary>
    /// Tags <paramref name="transaction"/> with Sam's <paramref name="tag"/>, as Sam would in the app.
    /// </summary>
    private Task TagAsSamAsync(string transaction, string tag) =>
        Classifications(Sam).AddCustomClassificationsToTransactionAsync(new AddCustomClassificationsToTransaction
        {
            TransactionId = Transaction(transaction),
            Classifications = [new SelectedCustomClassification { ClassificationId = Tag(tag) }]
        }, CancellationToken);

    /// <summary>
    /// Today's balance of <paramref name="accountName"/>, and its owner's daily total from all their accounts.
    /// </summary>
    private async Task AddBalanceAsync(string accountName, decimal current)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid accountId = Account(accountName);
        BankAccount account = context.BankAccounts.Single(x => x.Id == accountId);
        context.BalanceSnapshots.Add(new BalanceSnapshot(account.UserId, accountId, Today,
            new AccountBalance(current, current, DateTime.UtcNow), BalanceSnapshotSource.Synced, isStale: false));
        await context.SaveChangesAsync(CancellationToken);

        List<BalanceSnapshot> theirs = await context.BalanceSnapshots
            .Where(x => x.UserId == account.UserId && x.Date == Today)
            .ToListAsync(CancellationToken);
        NetWorthSnapshot? total = await context.NetWorthSnapshots
            .SingleOrDefaultAsync(x => x.UserId == account.UserId && x.Date == Today, CancellationToken);
        if (total is not null)
        {
            context.NetWorthSnapshots.Remove(total);
        }

        context.NetWorthSnapshots.Add(new NetWorthSnapshot(account.UserId, Today, theirs));
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

    /// <summary>
    /// The joint account as each person's connection names it, the same sort code and number either way.
    /// </summary>
    private static Func<Guid, Guid, BankAccount> Joint(string name) => (userId, connectionId) =>
    {
        BankAccount account = new(userId, connectionId, $"ext-{name}", "TRANSACTION", name, "GBP");
        account.UpdateIdentifier(AccountIdentifier.Create("11-22-33", "12345678", null));
        return account;
    };

    private static TransactionDetails Details(DateTime at, decimal amount, string description,
        bool labelled = false) =>
        new TransactionDetailsFaker().Generate() with
        {
            Amount = amount,
            Description = description,
            Type = amount < 0 ? TransactionType.Debit : TransactionType.Credit,
            Currency = "GBP",
            OccurredAt = at,
            IsPending = false,
            LabelledAsTransfer = labelled,
            Metadata = TransactionMetadata.None
        };

    private Guid Account(string name) => Scenario.AccountMappings[name];

    private Guid Transaction(string name) => Scenario.TransactionMappings[name];

    private Guid Tag(string name) => Scenario.ClassificationMappings[name];

    private AccountScope Scope(string username) => new(CurrentUserFor(username));

    private Task<List<AccountAndTransactions>> Accounts(string username) =>
        new AccountService(CurrentUserFor(username), DbContextFactory, NullLogger<AccountService>.Instance,
                Scope(username))
            .GetAccountsAndMostRecentTransactionsAsync(10, CancellationToken)
            .ToListAsync(CancellationToken).AsTask();

    private TransactionsService Transactions(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<TransactionsService>.Instance,
            TimeProvider.System, Scope(username));

    private ClassificationService Classifications(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<ClassificationService>.Instance, Scope(username),
            TimeProvider.System);

    private Task<List<Transaction>> SeenBy(string username) =>
        Transactions(username).GetAllTransactionsAsync(new FilterTransactions(), CancellationToken)
            .ToListAsync(CancellationToken).AsTask();

    private NetWorthService NetWorth(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<NetWorthService>.Instance, TimeProvider.System,
            Scope(username));

    private SharingService Sharing(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<SharingService>.Instance,
            _services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
}
