using FinanceTracker.Contracts.Account;
using FinanceTracker.Contracts.NetWorth;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.NetWorth;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account;
using FinanceTracker.Infrastructure.NetWorth;
using FinanceTracker.Infrastructure.Notifications;
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
/// A joint account Sam and Alex have both connected, Sam first. Sam shares their copy with Alex. Both copies have the
/// same supermarket shop, which Alex tagged Food on theirs. Alex also has a current account of their own. Both connect to
/// the same bank.
/// </summary>
public class JointAccountsTests : InfrastructureTestBase
{
    private const string Alex = "alex";
    private const string Sam = "sam";

    private static readonly DateTime Spent = DateTime.UtcNow.AddDays(-3);
    private static readonly DateOnly BalanceDay = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

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
            .Do(s => s.BankConnectionScenario.WithConnection("sam-bank", Sam, SameBank))
            .Do(s => s.BankAccountScenario.WithAccount("sam-joint", "sam-bank", Joint("Joint")))
            .Do(s => s.TransactionScenario.WithTransaction("shop-sam", "sam-joint", Details(-40m, "TESCO STORES")))
            .Do(s => s.BankConnectionScenario.WithConnection("alex-bank", Alex, SameBank))
            .Do(s => s.BankAccountScenario.WithAccount("alex-current", "alex-bank", Named("Alex current")))
            .Do(s => s.BankAccountScenario.WithAccount("alex-joint", "alex-bank", Joint("Joint account")))
            .Do(s => s.TransactionScenario.WithTransaction("shop-alex", "alex-joint", Details(-40m, "Tesco  Stores")))
            .Do(s => s.ClassificationScenario.WithClassification("food", Alex, "Food"))
            .Do(s => s.ClassificationScenario.WithClassification("groceries", Sam, "Groceries"));

        await TagAsync("shop-alex", "food");
        await AddBalanceAsync("alex-current", 500m);
        await AddBalanceAsync("sam-joint", 1_000m);
        await AddBalanceAsync("alex-joint", 1_000m);

        InviteOutcome sent = await Sharing(Sam).InviteAsync(new InviteToShare { Who = Alex }, CancellationToken);
        await Sharing(Alex).AcceptAsync(sent.Link!.Id, CancellationToken);
        _linkId = sent.Link.Id;
    }

    [After(Test)]
    public async Task TearDownAsync() => await _services.DisposeAsync();

    [Test]
    public async Task NothingShared_SuggestsNothing()
    {
        // Act
        LinkShares shares = (await Sharing(Alex).GetSharesAsync(_linkId, CancellationToken))!;

        // Assert
        await Assert.That(shares.JointSuggestions).IsEmpty();
    }

    [Test]
    public async Task OneOfThemShared_SuggestsMergingTheTwoCopies()
    {
        // Arrange
        await ShareSamsJointAsync();

        // Act
        LinkShares shares = (await Sharing(Alex).GetSharesAsync(_linkId, CancellationToken))!;
        List<UserLink> links = await Sharing(Alex).GetLinksAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(shares.JointSuggestions.Select(static x => (x.YourAccountId, x.TheirAccountId)))
                .IsEquivalentTo([(Account("alex-joint"), Account("sam-joint"))]);
            await Assert.That(links.Single().JointSuggestions).IsEqualTo(1);
        }
    }

    [Test]
    public async Task Merging_ShowsTheFirstConnectedCopyToBoth_WithBothPeoplesTags()
    {
        // Arrange
        await ShareSamsJointAsync();

        // Act
        LinkShares after = await MergeAsync();

        // Assert
        List<AccountAndTransactions> alexsAccounts = await Accounts(Alex);
        List<AccountAndTransactions> samsAccounts = await Accounts(Sam);
        Transaction asAlex = (await SeenBy(Alex)).Single(x => x.Description == "TESCO STORES");
        Transaction asSam = (await SeenBy(Sam)).Single();
        using (Assert.Multiple())
        {
            await Assert.That(after.JointSuggestions).IsEmpty();
            await Assert.That(after.JointAccounts.Select(static x => (x.Name, x.ShownIsYours)))
                .IsEquivalentTo([("Joint", false)]);
            await Assert.That(alexsAccounts.Select(static x => (x.AccountName, x.JointWith)))
                .IsEquivalentTo([("Alex current", (string?)null), ("Joint", "Sam Taylor")]);
            await Assert.That(samsAccounts.Select(static x => (x.AccountName, x.JointWith)))
                .IsEquivalentTo([("Joint", (string?)"Alex Smith")]);
            await Assert.That(asAlex.TransactionId).IsEqualTo(Transaction("shop-sam"));
            await Assert.That(asAlex.CustomTags).IsEquivalentTo(["Food"]);
            await Assert.That(asAlex.IsReadOnly).IsFalse();
            await Assert.That(asSam.CustomTags).IsEquivalentTo(["Food"]);
            await Assert.That(_raised.Select(static x => (x.UserId, x.Kind)))
                .Contains((Scenario.UserMappings[Sam], NotificationKind.JointAccount));
        }
    }

    [Test]
    public async Task Merged_JustMineStillShowsTheJointAccount()
    {
        // Arrange
        await ShareSamsJointAsync();
        await MergeAsync();
        await Sharing(Alex).ChangeViewAsync(new ChangeSharingView { IncludeShared = false }, CancellationToken);

        // Act
        List<AccountAndTransactions> accounts = await Accounts(Alex);

        // Assert
        await Assert.That(accounts.Select(static x => x.AccountName)).IsEquivalentTo(["Alex current", "Joint"]);
    }

    [Test]
    public async Task Merged_NetWorthCountsTheJointAccountOnce()
    {
        // Arrange
        await ShareSamsJointAsync();
        await MergeAsync();

        // Act
        NetWorthHistory history = await new NetWorthService(CurrentUserFor(Alex), DbContextFactory,
                NullLogger<NetWorthService>.Instance, TimeProvider.System, Scope(Alex))
            .GetNetWorthAsync(null, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(history.Points[^1].NetWorth).IsEqualTo(1_500m);
            await Assert.That(history.Breakdown.Select(static x => (x.Name, x.Amount)))
                .IsEquivalentTo([("Joint", 1_000m), ("Alex current", 500m)]);
        }
    }

    [Test]
    public async Task Merged_MoneyIntoTheJointAccountPairsWithTheShownCopy()
    {
        // Arrange
        await Scenario
            .Do(s => s.TransactionScenario.WithTransaction("alex-out", "alex-current",
                Details(-100m, "To joint", labelled: true)))
            .Do(s => s.TransactionScenario.WithTransaction("in-sam", "sam-joint",
                Details(100m, "From Alex", labelled: true)))
            .Do(s => s.TransactionScenario.WithTransaction("in-alex", "alex-joint",
                Details(100m, "From Alex", labelled: true)));
        await ShareSamsJointAsync();

        // Act
        await MergeAsync();

        // Assert
        BankTransaction outgoing = await LoadAsync("alex-out");
        using (Assert.Multiple())
        {
            await Assert.That(outgoing.Transfer!.PartnerTransactionId).IsEqualTo(Transaction("in-sam"));
            await Assert.That((await LoadAsync("in-sam")).Transfer!.PartnerTransactionId)
                .IsEqualTo(Transaction("alex-out"));
            await Assert.That((await LoadAsync("in-alex")).Transfer?.PartnerTransactionId).IsNull();
        }
    }

    [Test]
    public async Task Splitting_GivesAlexTheirCopyBack_WithTheTagsAsTheyStand()
    {
        // Arrange, Sam tags the shop while it's merged
        await ShareSamsJointAsync();
        LinkShares merged = await MergeAsync();
        await TagAsync("shop-sam", "groceries");

        // Act
        LinkShares after = await Sharing(Sam).SplitJointAccountAsync(_linkId,
            merged.JointAccounts.Single().JointAccountId, CancellationToken);

        // Assert
        List<AccountAndTransactions> accounts = await Accounts(Alex);
        Transaction alexsCopy = (await SeenBy(Alex)).Single(x => x.TransactionId == Transaction("shop-alex"));
        using (Assert.Multiple())
        {
            await Assert.That(after.JointAccounts).IsEmpty();
            await Assert.That(accounts.Select(static x => x.AccountName))
                .IsEquivalentTo(["Alex current", "Joint account", "Joint"]);
            await Assert.That(alexsCopy.CustomTags).IsEquivalentTo(["Food", "Groceries"]);
            await Assert.That(_raised.Select(static x => (x.UserId, x.Kind)))
                .Contains((Scenario.UserMappings[Alex], NotificationKind.JointAccount));
        }
    }

    [Test]
    public async Task EndingTheLink_SplitsIt_AndTakesSamsTagsOffAlexsCopy()
    {
        // Arrange
        await ShareSamsJointAsync();
        await MergeAsync();
        await TagAsync("shop-sam", "groceries");

        // Act
        await Sharing(Sam).EndAsync(_linkId, CancellationToken);

        // Assert
        List<AccountAndTransactions> accounts = await Accounts(Alex);
        BankTransaction alexsCopy = await LoadAsync("shop-alex");
        using (Assert.Multiple())
        {
            await Assert.That(accounts.Select(static x => x.AccountName))
                .IsEquivalentTo(["Alex current", "Joint account"]);
            await Assert.That(alexsCopy.CustomClassificationIds).IsEquivalentTo([Tag("food")]);
            await Assert.That((await LoadAsync("shop-sam")).CustomClassificationIds)
                .IsEquivalentTo([Tag("groceries")]);
        }
    }

    [Test]
    public async Task PotsWithoutAnAccountNumber_AreSuggestedByBankNameAndTheirTransactions()
    {
        // Arrange, pots of the joint account each of them has a copy of. Christmas has the same move on both, Garden
        // nothing yet but the same balance, Holiday a different name, and Rainy Day different moves
        await SeedPotsAsync();

        // Act
        LinkShares shares = (await Sharing(Alex).GetSharesAsync(_linkId, CancellationToken))!;

        // Assert
        await Assert.That(shares.JointSuggestions.Select(static x => (x.YourAccountName, x.TheirAccountName)))
            .IsEquivalentTo([("Joint account", "Joint"), ("Christmas", "Christmas"), ("Garden", "Garden")]);
    }

    [Test]
    public async Task MergingSeveralAtOnce_MergesEach()
    {
        // Arrange
        await SeedPotsAsync();

        // Act
        LinkShares after = await Sharing(Alex).MergeJointAccountsAsync(_linkId,
            Merge(("alex-joint", "sam-joint"), ("alex-christmas", "sam-christmas"), ("alex-garden", "sam-garden")),
            CancellationToken);

        // Assert
        List<AccountAndTransactions> accounts = await Accounts(Alex);
        using (Assert.Multiple())
        {
            await Assert.That(after.JointSuggestions).IsEmpty();
            await Assert.That(after.JointAccounts.Select(static x => x.Name))
                .IsEquivalentTo(["Joint", "Christmas", "Garden"]);
            await Assert.That(accounts.Count(static x => x.AccountName == "Christmas")).IsEqualTo(1);
            await Assert.That(accounts.Count(static x => x.AccountName == "Garden")).IsEqualTo(1);
        }
    }

    [Test]
    public async Task MergingAccountsThatArentTheSame_IsRefused()
    {
        // Arrange
        await ShareSamsJointAsync();

        // Act and assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => Sharing(Alex).MergeJointAccountsAsync(_linkId,
            Merge(("alex-current", "sam-joint")), CancellationToken));
    }

    /// <summary>
    /// Pots on both copies of the joint account, with no account number, Sam's shared with Alex.
    /// </summary>
    private async Task SeedPotsAsync()
    {
        await Scenario
            .Do(s => s.BankAccountScenario.WithAccount("sam-christmas", "sam-bank", Named("Christmas")))
            .Do(s => s.BankAccountScenario.WithAccount("sam-garden", "sam-bank", Named("Garden")))
            .Do(s => s.BankAccountScenario.WithAccount("sam-holiday", "sam-bank", Named("Holiday")))
            .Do(s => s.BankAccountScenario.WithAccount("sam-rainy", "sam-bank", Named("Rainy Day")))
            .Do(s => s.BankAccountScenario.WithAccount("alex-christmas", "alex-bank", Named("Christmas")))
            .Do(s => s.BankAccountScenario.WithAccount("alex-garden", "alex-bank", Named("Garden")))
            .Do(s => s.BankAccountScenario.WithAccount("alex-holidays", "alex-bank", Named("Holidays")))
            .Do(s => s.BankAccountScenario.WithAccount("alex-rainy", "alex-bank", Named("Rainy Day")))
            .Do(s => s.TransactionScenario.WithTransaction("christmas-sam", "sam-christmas", Details(-36m, "Christmas")))
            .Do(s => s.TransactionScenario.WithTransaction("christmas-alex", "alex-christmas", Details(-36m, "Christmas")))
            .Do(s => s.TransactionScenario.WithTransaction("holiday-sam", "sam-holiday", Details(20m, "Holiday")))
            .Do(s => s.TransactionScenario.WithTransaction("holiday-alex", "alex-holidays", Details(20m, "Holiday")))
            .Do(s => s.TransactionScenario.WithTransaction("rainy-sam", "sam-rainy", Details(10m, "Rainy Day")))
            .Do(s => s.TransactionScenario.WithTransaction("rainy-alex", "alex-rainy", Details(15m, "Rainy Day")));

        await Sharing(Sam).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares
        {
            Accounts = new[] { "sam-joint", "sam-christmas", "sam-garden", "sam-holiday", "sam-rainy" }
                .Select(x => new AccountShareChoice { AccountId = Account(x), Mode = ShareMode.ReadOnly })
                .ToList()
        }, CancellationToken);
    }

    private Task<LinkShares> ShareSamsJointAsync() =>
        Sharing(Sam).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares
        {
            Accounts = [new AccountShareChoice { AccountId = Account("sam-joint"), Mode = ShareMode.ReadOnly }]
        }, CancellationToken);

    private Task<LinkShares> MergeAsync() =>
        Sharing(Alex).MergeJointAccountsAsync(_linkId, Merge(("alex-joint", "sam-joint")), CancellationToken);

    private MergeJointAccounts Merge(params (string Yours, string Theirs)[] pairs) => new()
    {
        Accounts = pairs
            .Select(x => new MergeJointAccount { YourAccountId = Account(x.Yours), TheirAccountId = Account(x.Theirs) })
            .ToList()
    };

    /// <summary>
    /// Tags <paramref name="transaction"/> with <paramref name="tag"/>, as the tag's owner would.
    /// </summary>
    private async Task TagAsync(string transaction, string tag)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid transactionId = Transaction(transaction);
        Guid tagId = Tag(tag);
        BankTransaction stored = await context.BankTransactions.SingleAsync(x => x.Id == transactionId,
            CancellationToken);
        CustomClassification classification = await context.CustomClassifications.SingleAsync(x => x.Id == tagId,
            CancellationToken);
        stored.Classify(classification, stored.UserId);
        await context.SaveChangesAsync(CancellationToken);
    }

    private async Task AddBalanceAsync(string accountName, decimal current)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid accountId = Account(accountName);
        BankAccount account = context.BankAccounts.Single(x => x.Id == accountId);
        BalanceSnapshot balance = new(account.UserId, accountId, BalanceDay,
            new AccountBalance(current, current, BalanceDay.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)),
            BalanceSnapshotSource.Synced, isStale: false);
        context.BalanceSnapshots.Add(balance);
        await context.SaveChangesAsync(CancellationToken);

        // Each owner's own daily total, as the snapshot job keeps it, from all their own accounts
        List<BalanceSnapshot> theirs = await context.BalanceSnapshots
            .Where(x => x.UserId == account.UserId && x.Date == BalanceDay)
            .ToListAsync(CancellationToken);
        NetWorthSnapshot? total = await context.NetWorthSnapshots
            .SingleOrDefaultAsync(x => x.UserId == account.UserId && x.Date == BalanceDay, CancellationToken);
        if (total is not null)
        {
            context.NetWorthSnapshots.Remove(total);
        }

        context.NetWorthSnapshots.Add(new NetWorthSnapshot(account.UserId, BalanceDay, theirs));
        await context.SaveChangesAsync(CancellationToken);
    }

    private async Task<BankTransaction> LoadAsync(string transaction)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid id = Transaction(transaction);
        return await context.BankTransactions.AsNoTracking().SingleAsync(x => x.Id == id, CancellationToken);
    }

    /// <summary>
    /// A connection to the bank both of them use, so their pots can be told apart only by name and transactions.
    /// </summary>
    private static BankConnection SameBank(Guid userId) =>
        new(userId, "ob-bank", "The Bank", "code", BankConnectionFaker.Scopes, [], DateTime.UtcNow,
            DateTime.UtcNow.AddDays(90));

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

    private static TransactionDetails Details(decimal amount, string description, bool labelled = false) =>
        new TransactionDetailsFaker().Generate() with
        {
            Amount = amount,
            Description = description,
            Type = amount < 0 ? TransactionType.Debit : TransactionType.Credit,
            Currency = "GBP",
            OccurredAt = Spent,
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

    private Task<List<Transaction>> SeenBy(string username) =>
        new TransactionsService(CurrentUserFor(username), DbContextFactory, NullLogger<TransactionsService>.Instance,
                TimeProvider.System, Scope(username))
            .GetAllTransactionsAsync(new FilterTransactions(), CancellationToken)
            .ToListAsync(CancellationToken).AsTask();

    private SharingService Sharing(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<SharingService>.Instance,
            _services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
}
