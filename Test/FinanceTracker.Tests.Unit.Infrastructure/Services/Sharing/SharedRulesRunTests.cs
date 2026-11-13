using FinanceTracker.Contracts.Rules;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Infrastructure.Rules;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Infrastructure.Transactions;
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
using Transaction = FinanceTracker.Contracts.Transactions.Transaction;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Sharing;

/// <summary>
/// Rules running on an account shared with changes allowed. Sam shares Joint with Alex, letting them change it, and
/// keeps Gift fund private. Both have a supermarket shop on them. Alex has a Food tag, Sam a Groceries tag.
/// </summary>
public class SharedRulesRunTests : InfrastructureTestBase
{
    private const string Alex = "alex";
    private const string Sam = "sam";

    private static readonly DateTime Spent = DateTime.UtcNow.AddDays(-2);
    private static readonly DateTime LongAgo = DateTime.UtcNow.AddYears(-1);

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
            .Do(s => s.ClassificationScenario.WithClassification("food", Alex, "Food"))
            .Do(s => s.BankConnectionScenario.WithConnection("sam-bank", Sam))
            .Do(s => s.BankAccountScenario.WithAccount("sam-joint", "sam-bank", Named("Joint")))
            .Do(s => s.BankAccountScenario.WithAccount("sam-private", "sam-bank", Named("Gift fund")))
            .Do(s => s.TransactionScenario.WithTransaction("joint-shop", "sam-joint", Details("SUPERMARKET")))
            .Do(s => s.TransactionScenario.WithTransaction("private-shop", "sam-private", Details("SUPERMARKET")))
            .Do(s => s.ClassificationScenario.WithClassification("groceries", Sam, "Groceries"));

        InviteOutcome sent = await Sharing(Sam).InviteAsync(new InviteToShare { Who = Alex }, CancellationToken);
        await Sharing(Alex).AcceptAsync(sent.Link!.Id, CancellationToken);
        _linkId = sent.Link.Id;
        await ShareJointAsync(ShareMode.AllowChanges);
    }

    [After(Test)]
    public async Task TearDownAsync() => await _services.DisposeAsync();

    [Test]
    public async Task AlexsRule_RunsOnSamsSharedAccount_AndSamSeesItsTag()
    {
        // Arrange
        await AddRuleAsync(Alex, TagShops("food"));

        // Act
        await Rules(Alex).ApplyChangesAsync(LongAgo, CancellationToken);

        // Assert
        Transaction asSam = (await SeenBy(Sam)).Single(x => x.TransactionId == Transaction("joint-shop"));
        using (Assert.Multiple())
        {
            await Assert.That(asSam.CustomTags).IsEquivalentTo(["Food"]);
            await Assert.That((await LoadAsync("private-shop")).CustomClassificationIds).IsEmpty();
        }
    }

    [Test]
    public async Task BothPeoplesRules_EitherRunGivesBoth_AndNeitherUndoesTheOther()
    {
        // Arrange
        await AddRuleAsync(Alex, TagShops("food"));
        await AddRuleAsync(Sam, TagShops("groceries"));

        // Act, Sam runs after Alex, then Alex again
        await Rules(Alex).ApplyChangesAsync(LongAgo, CancellationToken);
        await Rules(Sam).ApplyChangesAsync(LongAgo, CancellationToken);
        await Rules(Alex).ApplyToExistingAsync(await RuleIdAsync(Alex), CancellationToken);

        // Assert
        await Assert.That((await LoadAsync("joint-shop")).CustomClassificationIds)
            .IsEquivalentTo([Tag("food"), Tag("groceries")]);
    }

    [Test]
    public async Task TheOwnersRule_WinsTheTransferAnswer()
    {
        // Arrange
        await AddRuleAsync(Alex, Answer(RuleActionType.MarkAsTransfer));
        await AddRuleAsync(Sam, Answer(RuleActionType.MarkNotATransfer));

        // Act
        await Rules(Alex).ApplyChangesAsync(LongAgo, CancellationToken);

        // Assert
        await Assert.That((await LoadAsync("joint-shop")).Transfer?.Decision).IsEqualTo(TransferDecision.NotATransfer);
    }

    [Test]
    public async Task ReadOnly_AlexsRuleDoesntReachIt()
    {
        // Arrange
        await ShareJointAsync(ShareMode.ReadOnly);
        await AddRuleAsync(Alex, TagShops("food"));

        // Act
        await Rules(Alex).ApplyChangesAsync(LongAgo, CancellationToken);

        // Assert
        await Assert.That((await LoadAsync("joint-shop")).CustomClassificationIds).IsEmpty();
    }

    [Test]
    public async Task NoLongerAllowedToChange_AlexsRuleTakesBackWhatItGave()
    {
        // Arrange
        await AddRuleAsync(Alex, TagShops("food"));
        await Rules(Alex).ApplyChangesAsync(LongAgo, CancellationToken);

        // Act
        await ShareJointAsync(ShareMode.ReadOnly);

        // Assert
        BankTransaction shop = await LoadAsync("joint-shop");
        using (Assert.Multiple())
        {
            await Assert.That(shop.CustomClassificationIds).IsEmpty();
            await Assert.That(shop.RuleTags).IsEmpty();
        }
    }

    [Test]
    public async Task NowAllowedToChange_AlexsRuleRunsStraightAway()
    {
        // Arrange
        await ShareJointAsync(ShareMode.ReadOnly);
        await AddRuleAsync(Alex, TagShops("food"));

        // Act
        await ShareJointAsync(ShareMode.AllowChanges);

        // Assert
        await Assert.That((await LoadAsync("joint-shop")).CustomClassificationIds).IsEquivalentTo([Tag("food")]);
    }

    [Test]
    public async Task EndingTheLink_TakesAlexsRuleTagOff()
    {
        // Arrange
        await AddRuleAsync(Alex, TagShops("food"));
        await Rules(Alex).ApplyChangesAsync(LongAgo, CancellationToken);

        // Act
        await Sharing(Sam).EndAsync(_linkId, CancellationToken);

        // Assert
        await Assert.That((await LoadAsync("joint-shop")).CustomClassificationIds).IsEmpty();
    }

    [Test]
    public async Task Preview_ShowsWhatItWouldDoToTheSharedAccount_NotThePrivateOne()
    {
        // Act
        RulePreview preview = await RulesService(Alex).PreviewRuleAsync(null, TagShops("food"), CancellationToken);

        // Assert
        await Assert.That(preview.Changes.Select(static x => (x.TransactionId, x.TagsAdded.Single())))
            .IsEquivalentTo([(Transaction("joint-shop"), "Food")]);
    }

    [Test]
    public async Task DraftFromASharedTransaction_StartsFromIt()
    {
        // Act
        SaveRule draft = await RulesService(Alex).GetDraftFromTransactionAsync(Transaction("joint-shop"),
            CancellationToken);

        // Assert
        await Assert.That(draft.Conditions.Select(static x => x.Value)).Contains("SUPERMARKET");
    }

    [Test]
    public async Task AConditionOnTheSharedAccount_IsAllowed()
    {
        // Act
        SaveRule onJoint = TagShops("food");
        onJoint.Conditions.Add(new RuleCondition
        {
            Field = RuleField.Account, Operator = RuleOperator.Equals, Value = Account("sam-joint").ToString()
        });
        await AddRuleAsync(Alex, onJoint);
        await Rules(Alex).ApplyChangesAsync(LongAgo, CancellationToken);

        // Assert
        await Assert.That((await LoadAsync("joint-shop")).CustomClassificationIds).IsEquivalentTo([Tag("food")]);
    }

    [Test]
    public async Task AConditionOnTheirPrivateAccount_IsRefused()
    {
        SaveRule onPrivate = TagShops("food");
        onPrivate.Conditions.Add(new RuleCondition
        {
            Field = RuleField.Account, Operator = RuleOperator.Equals, Value = Account("sam-private").ToString()
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RulesService(Alex).AddRuleAsync(onPrivate, CancellationToken));
    }

    private Task<LinkShares> ShareJointAsync(ShareMode mode) =>
        Sharing(Sam).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares
        {
            Accounts = [new AccountShareChoice { AccountId = Account("sam-joint"), Mode = mode }]
        }, CancellationToken);

    private Task<Rule> AddRuleAsync(string username, SaveRule rule) =>
        RulesService(username).AddRuleAsync(rule, CancellationToken);

    private async Task<Guid> RuleIdAsync(string username)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid userId = Scenario.UserMappings[username];
        return await context.Rules.Where(x => x.UserId == userId).Select(static x => x.Id)
            .SingleAsync(CancellationToken);
    }

    private SaveRule TagShops(string tag) => new()
    {
        Name = $"Shops are {tag}",
        Conditions = [new RuleCondition { Field = RuleField.Description, Operator = RuleOperator.Contains,
            Value = "SUPERMARKET" }],
        Actions = [new RuleAction { Type = RuleActionType.AddTag, ClassificationId = Tag(tag) }]
    };

    private static SaveRule Answer(RuleActionType answer) => new()
    {
        Name = $"Shops {answer}",
        Conditions = [new RuleCondition { Field = RuleField.Description, Operator = RuleOperator.Contains,
            Value = "SUPERMARKET" }],
        Actions = [new RuleAction { Type = answer }]
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

    private RuleApplicationService Rules(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<RuleApplicationService>.Instance,
            new TransferMatchingService(CurrentUserFor(username), DbContextFactory,
                NullLogger<TransferMatchingService>.Instance, TimeProvider.System, TestMetrics.Unobserved),
            Scope(username), TimeProvider.System, TestMetrics.Unobserved);

    private RulesService RulesService(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<RulesService>.Instance, Rules(username),
            Scope(username), TimeProvider.System);

    private Task<List<Transaction>> SeenBy(string username) =>
        new TransactionsService(CurrentUserFor(username), DbContextFactory, NullLogger<TransactionsService>.Instance,
                TimeProvider.System, Scope(username))
            .GetAllTransactionsAsync(new FilterTransactions(), CancellationToken)
            .ToListAsync(CancellationToken).AsTask();

    private SharingService Sharing(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<SharingService>.Instance,
            _services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
}
