using FinanceTracker.Contracts.Rules;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Infrastructure.Rules;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationKind = FinanceTracker.Domain.Notifications.NotificationKind;
using RuleAction = FinanceTracker.Contracts.Rules.RuleAction;
using RuleActionType = FinanceTracker.Contracts.Rules.RuleActionType;
using RuleCondition = FinanceTracker.Contracts.Rules.RuleCondition;
using RuleField = FinanceTracker.Contracts.Rules.RuleField;
using RuleOperator = FinanceTracker.Contracts.Rules.RuleOperator;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Sharing;

/// <summary>
/// Sam sharing a rule with Alex. Sam's Shops rule tags supermarket shops Groceries, on Joint, which Sam shares with
/// Alex letting them change it, and on Gift fund, which Sam keeps private. Sam also shares the Groceries tag with Alex
/// but not the Secret one, and Alex has a Food tag of their own.
/// </summary>
public class RuleSharingTests : InfrastructureTestBase
{
    private const string Alex = "alex";
    private const string Sam = "sam";

    private static readonly DateTime Spent = DateTime.UtcNow.AddDays(-2);

    private ServiceProvider _services = null!;
    private Guid _linkId;
    private Guid _shopsId;

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
            .Do(s => s.ClassificationScenario.WithClassification("food", Alex, "Food"))
            .Do(s => s.BankConnectionScenario.WithConnection("sam-bank", Sam))
            .Do(s => s.BankAccountScenario.WithAccount("sam-joint", "sam-bank", Named("Joint")))
            .Do(s => s.BankAccountScenario.WithAccount("sam-private", "sam-bank", Named("Gift fund")))
            .Do(s => s.TransactionScenario.WithTransaction("joint-shop", "sam-joint", Details("SUPERMARKET")))
            .Do(s => s.TransactionScenario.WithTransaction("private-shop", "sam-private", Details("SUPERMARKET")))
            .Do(s => s.ClassificationScenario.WithClassification("groceries", Sam, "Groceries"))
            .Do(s => s.ClassificationScenario.WithClassification("secret", Sam, "Secret"));

        InviteOutcome sent = await Sharing(Sam).InviteAsync(new InviteToShare { Who = Alex }, CancellationToken);
        await Sharing(Alex).AcceptAsync(sent.Link!.Id, CancellationToken);
        _linkId = sent.Link.Id;
        await Sharing(Sam).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares
        {
            Accounts = [new AccountShareChoice { AccountId = Account("sam-joint"), Mode = ShareMode.AllowChanges }]
        }, CancellationToken);

        Rule shops = await Rules(Sam).AddRuleAsync(Shops("groceries"), CancellationToken);
        _shopsId = shops.Id;
        await Application(Sam).ApplyToExistingAsync(_shopsId, CancellationToken);
    }

    [After(Test)]
    public async Task TearDownAsync() => await _services.DisposeAsync();

    [Test]
    public async Task Shared_AlexSeesItAfterTheirOwn_WithWhoseItIs_AndItsTagsName()
    {
        // Arrange
        await Rules(Alex).AddRuleAsync(Shops("food"), CancellationToken);

        // Act
        LinkShares saved = await ShareAsync(ShareMode.ReadOnly);

        // Assert
        List<Rule> alexs = await Rules(Alex).GetRulesAsync(CancellationToken).ToListAsync(CancellationToken);
        Rule shared = alexs[^1];
        using (Assert.Multiple())
        {
            await Assert.That(saved.YourItems.Where(static x => x.Kind == SharedItemKind.Rule)
                .Select(static x => (x.Name, x.Mode))).IsEquivalentTo([("Shops", (ShareMode?)ShareMode.ReadOnly)]);
            await Assert.That(alexs.Select(static x => x.SharedBy)).IsEquivalentTo([null, "Sam Taylor"],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(shared.Id).IsEqualTo(_shopsId);
            await Assert.That(shared.CanChange).IsFalse();
            await Assert.That(shared.Names[Tag("groceries")]).IsEqualTo("Groceries");
            await Assert.That(shared.TagChoices).IsEmpty();
        }
    }

    [Test]
    public async Task APrivateAccountItLooksAt_IsNamedPrivate()
    {
        // Arrange
        SaveRule onPrivate = Shops("groceries");
        onPrivate.Conditions.Add(new RuleCondition
        {
            Field = RuleField.Account, Operator = RuleOperator.Equals, Value = Account("sam-private").ToString()
        });
        await Rules(Sam).UpdateRuleAsync(_shopsId, onPrivate, CancellationToken);

        // Act
        await ShareAsync(ShareMode.ReadOnly);

        // Assert
        Rule shared = await Rules(Alex).GetRuleAsync(_shopsId, CancellationToken);
        await Assert.That(shared.Names[Account("sam-private")]).IsEqualTo(SharedRules.PrivateAccountName);
    }

    [Test]
    public async Task ReadOnly_AlexCantChangeIt()
    {
        // Arrange
        await ShareAsync(ShareMode.ReadOnly);
        SaveRule renamed = Shops("groceries");
        renamed.Name = "Supermarkets";

        // Act and assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Rules(Alex).UpdateRuleAsync(_shopsId, renamed, CancellationToken));
    }

    [Test]
    public async Task ChangesAllowed_AlexEditsIt_AndItStaysSams()
    {
        // Arrange
        await ShareAsync(ShareMode.AllowChanges);
        SaveRule renamed = Shops("groceries");
        renamed.Name = "Supermarkets";

        // Act
        Rule saved = await Rules(Alex).UpdateRuleAsync(_shopsId, renamed, CancellationToken);

        // Assert
        Rule asSam = (await Rules(Sam).GetRulesAsync(CancellationToken).ToListAsync(CancellationToken)).Single();
        using (Assert.Multiple())
        {
            await Assert.That(saved.SharedBy).IsEqualTo("Sam Taylor");
            await Assert.That(saved.TagChoices.Values).IsEquivalentTo(["Groceries"]);
            await Assert.That(asSam.Name).IsEqualTo("Supermarkets");
            await Assert.That(asSam.SharedBy).IsNull();
        }
    }

    [Test]
    [Arguments("secret")]
    [Arguments("food")]
    public async Task ChangesAllowed_OnlySamsTagsSharedWithAlexCanBeAdded(string tag)
    {
        // Arrange
        await ShareAsync(ShareMode.AllowChanges);
        SaveRule edited = Shops("groceries");
        edited.Actions.Add(new RuleAction { Type = RuleActionType.AddTag, ClassificationId = Tag(tag) });

        // Act and assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Rules(Alex).UpdateRuleAsync(_shopsId, edited, CancellationToken));
    }

    [Test]
    public async Task ChangesAllowed_OnlyItsOwnerDeletesIt()
    {
        // Arrange
        await ShareAsync(ShareMode.AllowChanges);

        // Act and assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Rules(Alex).DeleteRuleAsync(_shopsId, CancellationToken));
    }

    [Test]
    public async Task AlexSwitchingItOff_TakesBackWhatItGave_OnSamsPrivateAccountToo()
    {
        // Arrange
        await ShareAsync(ShareMode.AllowChanges);
        SaveRule off = Shops("groceries");
        off.IsEnabled = false;

        // Act
        await Rules(Alex).UpdateRuleAsync(_shopsId, off, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((await LoadAsync("joint-shop")).CustomClassificationIds).IsEmpty();
            await Assert.That((await LoadAsync("private-shop")).CustomClassificationIds).IsEmpty();
        }
    }

    [Test]
    public async Task AlexsPreview_OnlyShowsWhatAlexCanSee()
    {
        // Arrange, Sam's rule matches nothing now, Alex's edit would match shops again
        await ShareAsync(ShareMode.AllowChanges);
        await Rules(Sam).UpdateRuleAsync(_shopsId, Shops("groceries", "NOTHING MATCHES"), CancellationToken);
        SaveRule edited = Shops("groceries", "SUPER");

        // Act
        RulePreview preview = await Rules(Alex).PreviewRuleAsync(_shopsId, edited, CancellationToken);

        // Assert
        await Assert.That(preview.Changes.Select(static x => x.TransactionId))
            .IsEquivalentTo([Transaction("joint-shop")]);
    }

    [Test]
    public async Task Unshared_AlexNoLongerSeesIt()
    {
        // Arrange
        await ShareAsync(ShareMode.AllowChanges);

        // Act
        await Sharing(Sam).ChangeItemSharesAsync(_linkId, new ChangeItemShares
        {
            Items = [Item(SharedItemKind.Classification, Tag("groceries"), ShareMode.ReadOnly)]
        }, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(await Rules(Alex).GetRulesAsync(CancellationToken).ToListAsync(CancellationToken))
                .IsEmpty();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Rules(Alex).GetRuleAsync(_shopsId, CancellationToken));
        }
    }

    /// <summary>
    /// Sam shares Shops with Alex as <paramref name="mode"/>, with the Groceries tag read only.
    /// </summary>
    private Task<LinkShares> ShareAsync(ShareMode mode) =>
        Sharing(Sam).ChangeItemSharesAsync(_linkId, new ChangeItemShares
        {
            Items =
            [
                Item(SharedItemKind.Rule, _shopsId, mode),
                Item(SharedItemKind.Classification, Tag("groceries"), ShareMode.ReadOnly)
            ]
        }, CancellationToken);

    private static ItemShareChoice Item(SharedItemKind kind, Guid id, ShareMode mode) =>
        new() { Kind = kind, ItemId = id, Mode = mode };

    private SaveRule Shops(string tag, string description = "SUPERMARKET") => new()
    {
        Name = "Shops",
        Conditions = [new RuleCondition { Field = RuleField.Description, Operator = RuleOperator.Contains,
            Value = description }],
        Actions = [new RuleAction { Type = RuleActionType.AddTag, ClassificationId = Tag(tag) }]
    };

    private async Task<BankTransaction> LoadAsync(string transaction)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid id = Transaction(transaction);
        return await context.BankTransactions.AsNoTracking().SingleAsync(x => x.Id == id, CancellationToken);
    }

    private static Func<Guid, Guid, BankAccount> Named(string name) => (userId, connectionId) =>
        new BankAccount(userId, connectionId, $"ext-{name}", "TRANSACTION", name, "GBP");

    private static TransactionDetails Details(string description) =>
        new TransactionDetailsFaker().Generate() with
        {
            Amount = -40m,
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

    private Guid Tag(string name) => Scenario.ClassificationMappings[name];

    private AccountScope Scope(string username) => new(CurrentUserFor(username));

    private RuleApplicationService Application(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<RuleApplicationService>.Instance,
            new TransferMatchingService(CurrentUserFor(username), DbContextFactory,
                NullLogger<TransferMatchingService>.Instance, TimeProvider.System, TestMetrics.Unobserved),
            Scope(username), TimeProvider.System, TestMetrics.Unobserved);

    private RulesService Rules(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<RulesService>.Instance, Application(username),
            Scope(username), TimeProvider.System);

    private SharingService Sharing(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<SharingService>.Instance,
            _services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
}
