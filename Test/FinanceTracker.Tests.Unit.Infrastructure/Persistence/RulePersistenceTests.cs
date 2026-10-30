using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Infrastructure.Persistence;

public class RulePersistenceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private static readonly Guid TagId = Guid.CreateVersion7();

    [Test]
    public async Task Rule_RoundTripsThroughTheDatabase()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        Rule rule = new(Scenario.UserMappings[Username], "Groceries", 3, RuleMatchMode.Any,
        [
            new RuleCondition(RuleField.Description, RuleOperator.Contains, "GROCER", ignoreCase: false),
            new RuleCondition(RuleField.Amount, RuleOperator.Between, "5.00", "50.00")
        ],
        [new RuleAction(RuleActionType.AddTag, TagId), RuleAction.MarkNotATransfer()]);
        rule.StopProcessingAfterMatch(true);
        await SaveAsync(rule);

        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Rule loaded = await context.Rules.SingleAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(loaded.Name).IsEqualTo("Groceries");
            await Assert.That(loaded.Order).IsEqualTo(3);
            await Assert.That(loaded.IsEnabled).IsTrue();
            await Assert.That(loaded.MatchMode).IsEqualTo(RuleMatchMode.Any);
            await Assert.That(loaded.StopProcessing).IsTrue();
            await Assert.That(loaded.Conditions).IsEquivalentTo(rule.Conditions);
            await Assert.That(loaded.Actions).IsEquivalentTo(rule.Actions);
        }
    }

    [Test]
    public async Task Rule_NameConditionValuesAndBankClassifications_AreStoredAsCiphertext()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        await SaveAsync(new Rule(Scenario.UserMappings[Username], "Secret Rule", 0, RuleMatchMode.All,
            [
                new RuleCondition(RuleField.Counterparty, RuleOperator.Contains, "Secret Shop"),
                new RuleCondition(RuleField.Amount, RuleOperator.Between, "12.34", "56.78")
            ],
            [RuleAction.MarkAsTransfer(), RuleAction.HideClassification("SecretClassification")]));

        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        string actions = await context.Database
            .SqlQueryRaw<string>("SELECT \"Actions\" AS \"Value\" FROM \"Rules\"")
            .SingleAsync(CancellationToken);
        string name = await context.Database
            .SqlQueryRaw<string>("SELECT \"Name\" AS \"Value\" FROM \"Rules\"")
            .SingleAsync(CancellationToken);
        string conditions = await context.Database
            .SqlQueryRaw<string>("SELECT \"Conditions\" AS \"Value\" FROM \"Rules\"")
            .SingleAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(name).DoesNotContain("Secret");
            await Assert.That(conditions).DoesNotContain("Secret");
            await Assert.That(conditions).DoesNotContain("12.34");
            await Assert.That(conditions).DoesNotContain("56.78");
            await Assert.That(conditions).Contains("Counterparty");
            await Assert.That(actions).DoesNotContain("SecretClassification");
        }
    }

    [Test]
    public async Task Rule_ReplacedConditionsAndActions_AreSaved()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        Rule rule = new(Scenario.UserMappings[Username], "Rule", 0, RuleMatchMode.All,
            [new RuleCondition(RuleField.Description, RuleOperator.Contains, "OLD")],
            [RuleAction.MarkAsTransfer()]);
        await SaveAsync(rule);

        // Act
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            Rule tracked = await context.Rules.SingleAsync(CancellationToken);
            tracked.ReplaceConditions([new RuleCondition(RuleField.Description, RuleOperator.StartsWith, "NEW")]);
            tracked.ReplaceActions([new RuleAction(RuleActionType.AddTag, TagId)]);
            await context.SaveChangesAsync(CancellationToken);
        }

        // Assert
        await using FinanceTrackerContext verificationContext =
            await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Rule loaded = await verificationContext.Rules.SingleAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(loaded.Conditions)
                .IsEquivalentTo([new RuleCondition(RuleField.Description, RuleOperator.StartsWith, "NEW")]);
            await Assert.That(loaded.Actions).IsEquivalentTo([new RuleAction(RuleActionType.AddTag, TagId)]);
        }
    }

    [Test]
    public async Task Transaction_HiddenClassifications_AreStoredAsCiphertext()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("mortgage", "main",
                new TransactionDetailsFaker().Generate() with { ProviderClassifications = ["SecretClassification"] }));

        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            BankTransaction transaction = await context.BankTransactions.SingleAsync(CancellationToken);
            transaction.HideClassification("SecretClassification");
            await context.SaveChangesAsync(CancellationToken);
        }

        // Act
        await using FinanceTrackerContext verificationContext =
            await DbContextFactory.CreateDbContextAsync(CancellationToken);
        string stored = await verificationContext.Database
            .SqlQueryRaw<string>("SELECT \"HiddenClassifications\" AS \"Value\" FROM \"BankTransactions\"")
            .SingleAsync(CancellationToken);
        BankTransaction loaded = await verificationContext.BankTransactions.SingleAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(stored).DoesNotContain("SecretClassification");
            await Assert.That(loaded.HiddenClassifications.Single().Classification).IsEqualTo("SecretClassification");
        }
    }

    private async Task SaveAsync(Rule rule)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        context.Rules.Add(rule);
        await context.SaveChangesAsync(CancellationToken);
    }
}
