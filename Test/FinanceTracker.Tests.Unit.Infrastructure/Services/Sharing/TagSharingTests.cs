using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Classification;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Infrastructure.Transactions;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TagContract = FinanceTracker.Contracts.Classifications.Classification;
using ContractLink = FinanceTracker.Contracts.Sharing.UserLink;
using NotificationKind = FinanceTracker.Domain.Notifications.NotificationKind;
using Transaction = FinanceTracker.Contracts.Transactions.Transaction;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Sharing;

/// <summary>
/// Sam choosing to share tags with Alex. Sam has Holiday and Kids, Alex has Food and a coffee on their own account,
/// and Sam shares Joint with Alex, letting them change it.
/// </summary>
public class TagSharingTests : InfrastructureTestBase
{
    private const string Alex = "alex";
    private const string Sam = "sam";

    private static readonly DateTime Spent = DateTime.UtcNow.AddDays(-2);

    private ServiceProvider _services = null!;
    private Guid _linkId;

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
            .Do(s => s.TransactionScenario.WithTransaction("alex-coffee", "alex-current", Details(-3m, "Coffee")))
            .Do(s => s.ClassificationScenario.WithClassification("food", Alex, "Food"))
            .Do(s => s.BankConnectionScenario.WithConnection("sam-bank", Sam))
            .Do(s => s.BankAccountScenario.WithAccount("sam-joint", "sam-bank", Named("Joint")))
            .Do(s => s.TransactionScenario.WithTransaction("sam-shop", "sam-joint", Details(-40m, "Supermarket")))
            .Do(s => s.ClassificationScenario.WithClassification("holiday", Sam, "Holiday"))
            .Do(s => s.ClassificationScenario.WithClassification("kids", Sam, "Kids"));

        InviteOutcome sent = await Sharing(Sam).InviteAsync(new InviteToShare { Who = Alex }, CancellationToken);
        await Sharing(Alex).AcceptAsync(sent.Link!.Id, CancellationToken);
        _linkId = sent.Link.Id;
        await Sharing(Sam).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares
        {
            Accounts = [new AccountShareChoice { AccountId = Account("sam-joint"), Mode = ShareMode.AllowChanges }]
        }, CancellationToken);
    }

    [After(Test)]
    public async Task TearDownAsync() => await _services.DisposeAsync();

    [Test]
    public async Task Sharing_ListsTheTagForBoth_WhoSharesIt_AndHow()
    {
        // Act
        LinkShares saved = await ShareAsync(("holiday", ShareMode.ReadOnly));

        // Assert
        List<TagContract> alexs = await Tags(Alex);
        LinkShares asAlex = (await Sharing(Alex).GetSharesAsync(_linkId, CancellationToken))!;
        ContractLink link = await Sharing(Alex).GetLinksAsync(CancellationToken).SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(saved.YourItems.Select(static x => (x.Name, x.Mode)))
                .IsEquivalentTo([("Holiday", (ShareMode?)ShareMode.ReadOnly), ("Kids", null)]);
            await Assert.That(alexs.Select(static x => (x.Tag ?? string.Empty, x.SharedBy, x.CanChange)))
                .IsEquivalentTo([("Food", (string?)null, true), ("Holiday", "Sam Taylor", false)]);
            await Assert.That(asAlex.TheirItems.Select(static x => (x.Name, x.Mode)))
                .IsEquivalentTo([("Holiday", ShareMode.ReadOnly)]);
            await Assert.That(asAlex.YourItems.Select(static x => x.Name)).IsEquivalentTo(["Food"]);
            await Assert.That((link.ItemsTheyShare, link.ItemsYouShare, link.AccountsTheyShare)).IsEqualTo((1, 0, 1));
        }
    }

    [Test]
    public async Task SharedTag_CanGoOnTheirOwnTransactions_AndShowsByName()
    {
        // Arrange
        await ShareAsync(("holiday", ShareMode.ReadOnly));

        // Act
        await TagAsync(Alex, "alex-coffee", "holiday");

        // Assert
        Transaction coffee = (await SeenBy(Alex)).Single(x => x.Description == "Coffee");
        await Assert.That(coffee.CustomTags).IsEquivalentTo(["Holiday"]);
    }

    [Test]
    public async Task SharedTag_CanSplitTheirOwnTransactions()
    {
        // Arrange
        await ShareAsync(("holiday", ShareMode.ReadOnly));

        // Act
        await Transactions(Alex).SplitAsync(Transaction("alex-coffee"), new SplitTransaction
        {
            Parts =
            [
                new SplitTransactionPart { Amount = -2m, TagId = Tag("holiday") },
                new SplitTransactionPart { Amount = -1m, TagId = Tag("food") }
            ]
        }, CancellationToken);

        // Assert
        Transaction coffee = (await SeenBy(Alex)).Single(x => x.Description == "Coffee");
        await Assert.That(coffee.CustomTags).IsEquivalentTo(["Holiday", "Food"]);
    }

    [Test]
    public async Task NotShared_TheirTagCantBeUsed()
    {
        // Act
        await TagAsync(Alex, "alex-coffee", "kids");

        // Assert, an unknown tag is left off
        Transaction coffee = (await SeenBy(Alex)).Single(x => x.Description == "Coffee");
        await Assert.That(coffee.CustomTags).IsEmpty();
    }

    [Test]
    public async Task ReadOnly_CantBeRenamed()
    {
        // Arrange
        await ShareAsync(("holiday", ShareMode.ReadOnly));

        // Act and assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => Classifications(Alex)
            .RenameCustomClassificationAsync(Tag("holiday"), new RenameClassification { Tag = "Trips" },
                CancellationToken));
    }

    [Test]
    public async Task ChangesAllowed_CanBeRenamed_ButOnlyItsOwnerDeletesIt()
    {
        // Arrange
        await ShareAsync(("holiday", ShareMode.AllowChanges));

        // Act
        TagContract renamed = await Classifications(Alex).RenameCustomClassificationAsync(Tag("holiday"),
            new RenameClassification { Tag = "Trips" }, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(renamed.Tag).IsEqualTo("Trips");
            await Assert.That((await Tags(Sam)).Select(static x => x.Tag)).Contains("Trips");
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Classifications(Alex).RemoveCustomClassificationAsync(Tag("holiday"), CancellationToken));
        }
    }

    [Test]
    public async Task Unsharing_TakesItOffTheirTransactions()
    {
        // Arrange
        await ShareAsync(("holiday", ShareMode.ReadOnly));
        await TagAsync(Alex, "alex-coffee", "holiday");

        // Act
        await ShareAsync();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((await LoadAsync("alex-coffee")).CustomClassificationIds).IsEmpty();
            await Assert.That((await Tags(Alex)).Select(static x => x.Tag ?? string.Empty)).IsEquivalentTo(["Food"]);
        }
    }

    [Test]
    public async Task Unsharing_WhatSamPutOnAlexsTransactionStaysOn()
    {
        // Arrange, Sam tags the shared Joint with Holiday, and Alex's coffee through the shared tag
        await ShareAsync(("holiday", ShareMode.ReadOnly));
        await Sharing(Alex).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares
        {
            Accounts = [new AccountShareChoice { AccountId = Account("alex-current"), Mode = ShareMode.AllowChanges }]
        }, CancellationToken);
        await TagAsync(Sam, "alex-coffee", "holiday");

        // Act
        await ShareAsync();

        // Assert, Sam put it there, so Alex still sees its name on it
        Transaction coffee = (await SeenBy(Alex)).Single(x => x.Description == "Coffee");
        await Assert.That(coffee.CustomTags).IsEquivalentTo(["Holiday"]);
    }

    [Test]
    public async Task EndingTheLink_TakesItOffTheirTransactions()
    {
        // Arrange
        await ShareAsync(("holiday", ShareMode.ReadOnly));
        await TagAsync(Alex, "alex-coffee", "holiday");

        // Act
        await Sharing(Alex).EndAsync(_linkId, CancellationToken);

        // Assert
        await Assert.That((await LoadAsync("alex-coffee")).CustomClassificationIds).IsEmpty();
    }

    [Test]
    public async Task SharingSomeoneElsesTag_IsRefused()
    {
        // Act and assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => Sharing(Alex).ChangeItemSharesAsync(_linkId,
            new ChangeItemShares
            {
                Items =
                [
                    new ItemShareChoice
                    {
                        Kind = SharedItemKind.Classification, ItemId = Tag("holiday"), Mode = ShareMode.ReadOnly
                    }
                ]
            }, CancellationToken));
    }

    [Test]
    public async Task ChoosingATagAlreadyOnTheirTransaction_KeepsBothShares()
    {
        // Arrange, Sam tags the shared Joint for themself, then tags Alex's coffee with it
        await Sharing(Alex).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares
        {
            Accounts = [new AccountShareChoice { AccountId = Account("alex-current"), Mode = ShareMode.AllowChanges }]
        }, CancellationToken);
        await TagAsync(Sam, "alex-coffee", "holiday");

        // Act
        await ShareAsync(("holiday", ShareMode.AllowChanges));

        // Assert
        List<TagContract> alexs = await Tags(Alex);
        await Assert.That(alexs.Single(x => x.Tag == "Holiday").CanChange).IsTrue();
    }

    /// <summary>
    /// Sam shares exactly <paramref name="tags"/> with Alex.
    /// </summary>
    private Task<LinkShares> ShareAsync(params (string Tag, ShareMode Mode)[] tags) =>
        Sharing(Sam).ChangeItemSharesAsync(_linkId, new ChangeItemShares
        {
            Items = tags.Select(x => new ItemShareChoice
                {
                    Kind = SharedItemKind.Classification, ItemId = Tag(x.Tag), Mode = x.Mode
                })
                .ToList()
        }, CancellationToken);

    private Task TagAsync(string username, string transaction, string tag) =>
        Classifications(username).AddCustomClassificationsToTransactionAsync(new AddCustomClassificationsToTransaction
        {
            TransactionId = Transaction(transaction),
            Classifications = [new SelectedCustomClassification { ClassificationId = Tag(tag) }]
        }, CancellationToken);

    private async Task<BankTransaction> LoadAsync(string transaction)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid id = Transaction(transaction);
        return await context.BankTransactions.AsNoTracking().SingleAsync(x => x.Id == id, CancellationToken);
    }

    private Task<List<TagContract>> Tags(string username) =>
        Classifications(username).GetAllCustomClassificationsAsync(CancellationToken)
            .ToListAsync(CancellationToken).AsTask();

    private static Func<Guid, Guid, BankAccount> Named(string name) => (userId, connectionId) =>
        new BankAccount(userId, connectionId, $"ext-{name}", "TRANSACTION", name, "GBP");

    private static TransactionDetails Details(decimal amount, string description) =>
        new TransactionDetailsFaker().Generate() with
        {
            Amount = amount,
            Description = description,
            Type = amount < 0 ? TransactionType.Debit : TransactionType.Credit,
            Currency = "GBP",
            OccurredAt = Spent,
            IsPending = false,
            LabelledAsTransfer = false,
            Metadata = TransactionMetadata.None
        };

    private Guid Account(string name) => Scenario.AccountMappings[name];

    private Guid Transaction(string name) => Scenario.TransactionMappings[name];

    private Guid Tag(string name) => Scenario.ClassificationMappings[name];

    private AccountScope Scope(string username) => new(CurrentUserFor(username));

    private ClassificationService Classifications(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<ClassificationService>.Instance, Scope(username),
            TimeProvider.System);

    private TransactionsService Transactions(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<TransactionsService>.Instance,
            TimeProvider.System, Scope(username));

    private Task<List<Transaction>> SeenBy(string username) =>
        Transactions(username).GetAllTransactionsAsync(new FilterTransactions(), CancellationToken)
            .ToListAsync(CancellationToken).AsTask();

    private SharingService Sharing(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<SharingService>.Instance,
            _services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
}
