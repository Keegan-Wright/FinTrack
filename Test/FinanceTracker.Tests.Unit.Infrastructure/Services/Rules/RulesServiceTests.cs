using FinanceTracker.Contracts.Rules;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Infrastructure.Rules;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using DomainRule = FinanceTracker.Domain.Rules.Rule;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Rules;

public class RulesServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    [Test]
    public async Task AddRuleAsync_RunsAfterTheUsersOtherRules_AndLeavesHistoryAlone()
    {
        // Arrange
        await SeedAsync(Username)
            .Do(s => s.RuleScenario.WithRule("existing", Username, userId =>
            {
                DomainRule rule = new RuleFaker(userId).Generate();
                rule.MoveTo(4);
                return rule;
            }))
            .Do(s => s.TransactionScenario.WithTransaction("history", "main", Details("GROCER STORE")));

        // Act
        Rule added = await BuildSubject(Username).AddRuleAsync(TagGroceries("shopping"), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(added.Order).IsEqualTo(5);
            await Assert.That(added.IsEnabled).IsTrue();
            await Assert.That(added.Conditions.Single().Value).IsEqualTo("GROCER STORE");
            await Assert.That((await LoadTransactionAsync("history")).CustomClassificationIds).IsEmpty();
        }
    }

    [Test]
    public async Task AddRuleAsync_FirstRule_RunsFirst()
    {
        // Arrange
        await SeedAsync(Username);

        // Act
        Rule added = await BuildSubject(Username).AddRuleAsync(TagGroceries("shopping"), CancellationToken);

        // Assert
        await Assert.That(added.Order).IsEqualTo(0);
    }

    [Test]
    public async Task AddRuleAsync_AnotherUsersTag_Throws()
    {
        // Arrange
        await SeedAsync(Username);
        await Scenario.Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.ClassificationScenario.WithClassification("theirs", OtherUsername));

        // Act & Assert
        await Assert.That(async () =>
                await BuildSubject(Username).AddRuleAsync(TagGroceries("theirs"), CancellationToken))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task AddRuleAsync_OnTheUsersAccount_IsSaved()
    {
        // Arrange
        await SeedAsync(Username);
        SaveRule rule = TagGroceries("shopping");
        rule.Conditions.Add(OnAccount(Scenario.AccountMappings["main"]));

        // Act
        Rule added = await BuildSubject(Username).AddRuleAsync(rule, CancellationToken);

        // Assert
        await Assert.That(added.Conditions[1].Value).IsEqualTo(Scenario.AccountMappings["main"].ToString());
    }

    [Test]
    public async Task AddRuleAsync_OnAnotherUsersAccount_Throws()
    {
        // Arrange
        await SeedAsync(Username);
        await Scenario.Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername))
            .Do(s => s.BankAccountScenario.WithAccount("theirs", "theirs"));
        SaveRule rule = TagGroceries("shopping");
        rule.Conditions.Add(OnAccount(Scenario.AccountMappings["theirs"]));

        // Act & Assert
        await Assert.That(async () => await BuildSubject(Username).AddRuleAsync(rule, CancellationToken))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetRulesAsync_OnlyTheUsers_InTheOrderTheyRun()
    {
        // Arrange
        await SeedAsync(Username)
            .Do(s => s.RuleScenario.WithRule("second", Username, userId => Ordered(userId, "Second", 2)))
            .Do(s => s.RuleScenario.WithRule("first", Username, userId => Ordered(userId, "First", 1)));
        await Scenario.Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.RuleScenario.WithRule("theirs", OtherUsername));

        // Act
        List<Rule> rules = await BuildSubject(Username).GetRulesAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        await Assert.That(rules.Select(static x => x.Name)).IsEquivalentTo(["First", "Second"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task GetRuleAsync_AnotherUsersRule_Throws()
    {
        // Arrange
        await SeedAsync(Username);
        await Scenario.Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.RuleScenario.WithRule("theirs", OtherUsername));

        // Act & Assert
        await Assert.That(async () => await BuildSubject(Username).GetRuleAsync(Scenario.RuleMappings["theirs"],
            CancellationToken)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task UpdateRuleAsync_NoLongerMatching_TakesBackItsTag()
    {
        // Arrange
        await SeedAsync(Username)
            .Do(s => s.TransactionScenario.WithTransaction("reached", "main", Details("GROCER STORE")));
        RulesService subject = BuildSubject(Username);
        Rule rule = await subject.AddRuleAsync(TagGroceries("shopping"), CancellationToken);
        await BuildApplication(Username).ApplyChangesAsync(DateTime.MinValue, CancellationToken);

        SaveRule edited = TagGroceries("shopping");
        edited.Conditions[0].Value = "CAFE";

        // Act
        await subject.UpdateRuleAsync(rule.Id, edited, CancellationToken);

        // Assert
        BankTransaction reached = await LoadTransactionAsync("reached");
        using (Assert.Multiple())
        {
            await Assert.That(reached.CustomClassificationIds).IsEmpty();
            await Assert.That((await subject.GetRuleAsync(rule.Id, CancellationToken)).Conditions.Single().Value)
                .IsEqualTo("CAFE");
        }
    }

    [Test]
    public async Task UpdateRuleAsync_AnotherUsersRule_Throws()
    {
        // Arrange
        await SeedAsync(Username);
        await Scenario.Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.RuleScenario.WithRule("theirs", OtherUsername));

        // Act & Assert
        await Assert.That(async () => await BuildSubject(Username).UpdateRuleAsync(Scenario.RuleMappings["theirs"],
            TagGroceries("shopping"), CancellationToken)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task DeleteRuleAsync_TakesBackWhatItGave_AndALaterRuleTakesOver()
    {
        // Arrange
        await SeedAsync(Username)
            .Do(s => s.TransactionScenario.WithTransaction("reached", "main", Details("GROCER STORE")));
        RulesService subject = BuildSubject(Username);
        Rule first = await subject.AddRuleAsync(TagGroceries("shopping"), CancellationToken);
        Rule second = await subject.AddRuleAsync(TagGroceries("shopping"), CancellationToken);
        await BuildApplication(Username).ApplyChangesAsync(DateTime.MinValue, CancellationToken);

        // Act
        await subject.DeleteRuleAsync(first.Id, CancellationToken);

        // Assert
        BankTransaction reached = await LoadTransactionAsync("reached");
        using (Assert.Multiple())
        {
            await Assert.That(reached.CustomClassificationIds)
                .IsEquivalentTo([Scenario.ClassificationMappings["shopping"]]);
            await Assert.That(reached.RuleTags.Single().RuleId).IsEqualTo(second.Id);
            await Assert.That(await subject.GetRulesAsync(CancellationToken).CountAsync(CancellationToken))
                .IsEqualTo(1);
        }
    }

    [Test]
    public async Task ReorderRulesAsync_SetsTheOrder()
    {
        // Arrange
        await SeedAsync(Username)
            .Do(s => s.RuleScenario.WithRule("a", Username, userId => Ordered(userId, "A", 0)))
            .Do(s => s.RuleScenario.WithRule("b", Username, userId => Ordered(userId, "B", 1)));
        RulesService subject = BuildSubject(Username);

        // Act
        await subject.ReorderRulesAsync(new ReorderRules
        {
            RuleIds = [Scenario.RuleMappings["b"], Scenario.RuleMappings["a"]]
        }, CancellationToken);

        // Assert
        List<Rule> rules = await subject.GetRulesAsync(CancellationToken).ToListAsync(CancellationToken);
        await Assert.That(rules.Select(static x => x.Name)).IsEquivalentTo(["B", "A"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task ReorderRulesAsync_MissingARule_Throws()
    {
        // Arrange
        await SeedAsync(Username)
            .Do(s => s.RuleScenario.WithRule("a", Username))
            .Do(s => s.RuleScenario.WithRule("b", Username));

        // Act & Assert
        await Assert.That(() => BuildSubject(Username).ReorderRulesAsync(
                new ReorderRules { RuleIds = [Scenario.RuleMappings["a"]] }, CancellationToken))
            .Throws<InvalidOperationException>();
    }

    private Task<TestingScenario> SeedAsync(string username) =>
        Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.ClassificationScenario.WithClassification("shopping", username, "Shopping"));

    private SaveRule TagGroceries(string tagName) => new()
    {
        Name = "Groceries",
        Conditions = [new RuleCondition { Field = RuleField.Description, Operator = RuleOperator.Contains,
            Value = "GROCER STORE" }],
        Actions = [new RuleAction { Type = RuleActionType.AddTag,
            ClassificationId = Scenario.ClassificationMappings[tagName] }]
    };

    private static RuleCondition OnAccount(Guid accountId) =>
        new() { Field = RuleField.Account, Operator = RuleOperator.Equals, Value = accountId.ToString() };

    private static DomainRule Ordered(Guid userId, string name, int order)
    {
        DomainRule rule = new RuleFaker(userId).Generate();
        rule.Rename(name);
        rule.MoveTo(order);
        return rule;
    }

    private static TransactionDetails Details(string description) =>
        new TransactionDetailsFaker().Generate() with
        {
            Description = description,
            Amount = -12.5m,
            Type = TransactionType.Debit,
            LabelledAsTransfer = false
        };

    private async Task<BankTransaction> LoadTransactionAsync(string name)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid id = Scenario.TransactionMappings[name];
        return await context.BankTransactions.SingleAsync(x => x.Id == id, CancellationToken);
    }

    private RuleApplicationService BuildApplication(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<RuleApplicationService>.Instance,
            new TransferMatchingService(CurrentUserFor(username), DbContextFactory,
                NullLogger<TransferMatchingService>.Instance, TimeProvider.System),
            TimeProvider.System);

    private RulesService BuildSubject(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<RulesService>.Instance,
            BuildApplication(username));
}
