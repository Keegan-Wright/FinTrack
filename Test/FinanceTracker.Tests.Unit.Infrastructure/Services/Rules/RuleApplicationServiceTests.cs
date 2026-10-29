using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Infrastructure.Rules;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Rules;

public class RuleApplicationServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    [Test]
    public async Task ApplyChangesAsync_FirstRun_ReachesOnlyTheSync()
    {
        // Arrange
        await SeedAsync(Username, "mine")
            .Do(s => s.RuleScenario.WithRule("groceries", Username, TagsGroceries))
            .Do(s => s.TransactionScenario.WithTransaction("history", "mine", Details("GROCER STORE 1")));
        DateTime syncStartedAt = DateTime.UtcNow;
        await Scenario.Do(s => s.TransactionScenario.WithTransaction("synced", "mine", Details("GROCER STORE 2")));

        // Act
        int changed = await BuildSubject(Username).ApplyChangesAsync(syncStartedAt, CancellationToken);

        // Assert
        Dictionary<Guid, BankTransaction> stored = await LoadTransactionsAsync();
        using (Assert.Multiple())
        {
            await Assert.That(changed).IsEqualTo(1);
            await Assert.That(stored[Scenario.TransactionMappings["synced"]].CustomClassificationIds)
                .IsEquivalentTo([Scenario.ClassificationMappings["mine"]]);
            await Assert.That(stored[Scenario.TransactionMappings["history"]].CustomClassificationIds).IsEmpty();
        }
    }

    [Test]
    public async Task ApplyChangesAsync_LaterRun_StartsFromTheLastRun()
    {
        // Arrange
        await SeedAsync(Username, "mine")
            .Do(s => s.RuleScenario.WithRule("groceries", Username, TagsGroceries));
        RuleApplicationService subject = BuildSubject(Username);
        await subject.ApplyChangesAsync(DateTime.UtcNow, CancellationToken);
        await Scenario.Do(s => s.TransactionScenario.WithTransaction("synced", "mine", Details("GROCER STORE")));

        // Act, a first run from here would miss it
        int changed = await subject.ApplyChangesAsync(DateTime.MaxValue, CancellationToken);

        // Assert
        await Assert.That(changed).IsEqualTo(1);
    }

    [Test]
    public async Task ApplyChangesAsync_PendingTransaction_RecordsTheRulesTransferAnswer()
    {
        // Arrange
        await SeedAsync(Username, "mine")
            .Do(s => s.RuleScenario.WithRule("savings", Username, userId => new Rule(userId, "Savings", 0,
                RuleMatchMode.All, [new RuleCondition(RuleField.Description, RuleOperator.Contains, "INVEST")],
                [RuleAction.MarkAsTransfer()])))
            .Do(s => s.TransactionScenario.WithTransaction("pending", "mine",
                Details("INVEST PLATFORM", pending: true)));

        // Act
        await BuildSubject(Username).ApplyChangesAsync(DateTime.MinValue, CancellationToken);

        // Assert
        TransferInfo? transfer = (await LoadTransactionsAsync())[Scenario.TransactionMappings["pending"]].Transfer;
        using (Assert.Multiple())
        {
            await Assert.That(transfer!.Decision).IsEqualTo(TransferDecision.Transfer);
            await Assert.That(transfer.Source).IsEqualTo(DecisionSource.Rule);
            await Assert.That(transfer.RuleId).IsEqualTo(Scenario.RuleMappings["savings"]);
        }
    }

    [Test]
    public async Task ApplyChangesAsync_NoEnabledRules_StillMovesOn()
    {
        // Arrange
        await SeedAsync(Username, "mine")
            .Do(s => s.RuleScenario.WithRule("groceries", Username, userId =>
            {
                Rule rule = TagsGroceries(userId);
                rule.Disable();
                return rule;
            }))
            .Do(s => s.TransactionScenario.WithTransaction("synced", "mine", Details("GROCER STORE")));
        RuleApplicationService subject = BuildSubject(Username);
        await subject.ApplyChangesAsync(DateTime.MinValue, CancellationToken);
        await EnableRuleAsync(Scenario.RuleMappings["groceries"]);

        // Act
        int changed = await subject.ApplyChangesAsync(DateTime.MinValue, CancellationToken);

        // Assert, enabling a rule doesn't reach back over what synced while it was off
        await Assert.That(changed).IsEqualTo(0);
    }

    [Test]
    public async Task ApplyChangesAsync_AnotherUsersRules_DontApply()
    {
        // Arrange
        await SeedAsync(Username, "mine")
            .Do(s => s.TransactionScenario.WithTransaction("synced", "mine", Details("GROCER STORE")));
        await SeedAsync(OtherUsername, "theirs")
            .Do(s => s.RuleScenario.WithRule("groceries", OtherUsername, TagsGroceries));

        // Act
        int changed = await BuildSubject(Username).ApplyChangesAsync(DateTime.MinValue, CancellationToken);

        // Assert
        await Assert.That(changed).IsEqualTo(0);
    }

    [Test]
    public async Task ReapplyAsync_RuleDisabled_TakesBackItsTag()
    {
        // Arrange
        await SeedAsync(Username, "mine")
            .Do(s => s.RuleScenario.WithRule("groceries", Username, TagsGroceries))
            .Do(s => s.TransactionScenario.WithTransaction("reached", "mine", Details("GROCER STORE")))
            .Do(s => s.TransactionScenario.WithTransaction("untouched", "mine", Details("GROCER CAFE")));
        RuleApplicationService subject = BuildSubject(Username);
        await subject.ApplyChangesAsync(DateTime.MinValue, CancellationToken);
        await DisableRuleAsync(Scenario.RuleMappings["groceries"]);

        // Act
        int changed = await subject.ReapplyAsync(Scenario.RuleMappings["groceries"], CancellationToken);

        // Assert
        Dictionary<Guid, BankTransaction> stored = await LoadTransactionsAsync();
        using (Assert.Multiple())
        {
            await Assert.That(changed).IsEqualTo(1);
            await Assert.That(stored[Scenario.TransactionMappings["reached"]].CustomClassificationIds).IsEmpty();
            await Assert.That(stored[Scenario.TransactionMappings["reached"]].RuleTags).IsEmpty();
        }
    }

    private Task<TestingScenario> SeedAsync(string username, string prefix) =>
        Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.BankConnectionScenario.WithConnection(prefix, username))
            .Do(s => s.BankAccountScenario.WithAccount(prefix, prefix))
            .Do(s => s.ClassificationScenario.WithClassification(prefix, username, "Shopping"));

    /// <summary>
    /// Adds the user's tag, the one <see cref="SeedAsync"/> made with their prefix, to anything from a grocer.
    /// </summary>
    private Rule TagsGroceries(Guid userId)
    {
        string prefix = userId == Scenario.UserMappings[Username] ? "mine" : "theirs";
        return new Rule(userId, "Groceries", 0, RuleMatchMode.All,
            [new RuleCondition(RuleField.Description, RuleOperator.Contains, "GROCER STORE")],
            [new RuleAction(RuleActionType.AddTag, Scenario.ClassificationMappings[prefix])]);
    }

    private static TransactionDetails Details(string description, bool pending = false) =>
        new TransactionDetailsFaker().Generate() with
        {
            Description = description,
            Amount = -12.5m,
            Type = TransactionType.Debit,
            IsPending = pending,
            LabelledAsTransfer = false
        };

    private async Task EnableRuleAsync(Guid ruleId)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        (await context.Rules.SingleAsync(x => x.Id == ruleId, CancellationToken)).Enable();
        await context.SaveChangesAsync(CancellationToken);
    }

    private async Task DisableRuleAsync(Guid ruleId)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        (await context.Rules.SingleAsync(x => x.Id == ruleId, CancellationToken)).Disable();
        await context.SaveChangesAsync(CancellationToken);
    }

    private async Task<Dictionary<Guid, BankTransaction>> LoadTransactionsAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.BankTransactions.ToDictionaryAsync(static x => x.Id, CancellationToken);
    }

    private RuleApplicationService BuildSubject(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<RuleApplicationService>.Instance,
            new TransferMatchingService(CurrentUserFor(username), DbContextFactory,
                NullLogger<TransferMatchingService>.Instance, TimeProvider.System),
            TimeProvider.System);
}
