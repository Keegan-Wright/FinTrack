using FinanceTracker.Client.Components.Rules;
using FinanceTracker.Contracts.Rules;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Tests.Unit.Client.Rules;

public class RuleTextTests
{
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid TagId = Guid.NewGuid();
    private static readonly Dictionary<Guid, string> AccountNames = new() { [AccountId] = "Current account" };
    private static readonly Dictionary<Guid, string> TagNames = new() { [TagId] = "Shopping" };

    [Test]
    public async Task Describe_TextCondition_QuotesTheValue() =>
        await Assert.That(RuleText.Describe(Condition(RuleField.Counterparty, RuleOperator.Contains, "tesco"),
            AccountNames)).IsEqualTo("Merchant or payee contains \"tesco\"");

    [Test]
    public async Task Describe_AmountRange_ShowsBothAmountsAsMoney()
    {
        // Arrange
        using CultureScope culture = new();
        RuleCondition condition = Condition(RuleField.Amount, RuleOperator.Between, "5", "50.00");

        // Act
        string text = RuleText.Describe(condition, AccountNames);

        // Assert
        await Assert.That(text).IsEqualTo("Amount is between £5.00 and £50.00");
    }

    [Test]
    [Arguments("Debit", "Money in or out is money out")]
    [Arguments("credit", "Money in or out is money in")]
    public async Task Describe_Type_SaysWhichWay(string value, string expected) =>
        await Assert.That(RuleText.Describe(Condition(RuleField.Type, RuleOperator.Equals, value), AccountNames))
            .IsEqualTo(expected);

    [Test]
    public async Task Describe_Account_UsesItsNameOrSaysItsGone()
    {
        // Act
        string known = RuleText.Describe(Condition(RuleField.Account, RuleOperator.Equals, AccountId.ToString()),
            AccountNames);
        string removed = RuleText.Describe(Condition(RuleField.Account, RuleOperator.Equals,
            Guid.NewGuid().ToString()), AccountNames);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(known).IsEqualTo("Account is Current account");
            await Assert.That(removed).IsEqualTo("Account is a removed account");
        }
    }

    [Test]
    public async Task Describe_Actions_NameTheTagOrSayItsGone()
    {
        // Act
        string tag = RuleText.Describe(new RuleAction { Type = RuleActionType.AddTag, ClassificationId = TagId },
            TagNames);
        string deleted = RuleText.Describe(
            new RuleAction { Type = RuleActionType.AddTag, ClassificationId = Guid.NewGuid() }, TagNames);
        string transfer = RuleText.Describe(new RuleAction { Type = RuleActionType.MarkAsTransfer }, TagNames);
        string hide = RuleText.Describe(
            new RuleAction { Type = RuleActionType.HideClassification, BankClassification = "TV" }, TagNames);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(tag).IsEqualTo("Add tag Shopping");
            await Assert.That(deleted).IsEqualTo("Add a deleted tag");
            await Assert.That(transfer).IsEqualTo("Mark as a transfer");
            await Assert.That(hide).IsEqualTo("Hide bank classification TV");
        }
    }

    [Test]
    [Arguments(RuleMatchMode.All, "Merchant or payee contains \"a\" and Description contains \"b\" → Add tag Shopping")]
    [Arguments(RuleMatchMode.Any, "Merchant or payee contains \"a\" or Description contains \"b\" → Add tag Shopping")]
    public async Task Summary_JoinsConditionsByTheMatchMode(RuleMatchMode matchMode, string expected)
    {
        // Arrange
        Rule rule = RuleWith(matchMode, [new RuleAction { Type = RuleActionType.AddTag, ClassificationId = TagId }]);

        // Act
        string summary = RuleText.Summary(rule, AccountNames, TagNames);

        // Assert
        await Assert.That(summary).IsEqualTo(expected);
    }

    [Test]
    public async Task Problem_NoActions_SaysToGiveItOne() =>
        await Assert.That(RuleText.Problem(RuleWith(RuleMatchMode.All, []), AccountNames))
            .IsEqualTo("Its tag was deleted, give it an action to turn it back on");

    [Test]
    public async Task Problem_RemovedAccount_SaysItCantMatch()
    {
        // Arrange
        Rule rule = new()
        {
            Name = "Card",
            Conditions = [Condition(RuleField.Account, RuleOperator.Equals, Guid.NewGuid().ToString())],
            Actions = [new RuleAction { Type = RuleActionType.MarkNotATransfer }]
        };

        // Act
        string? problem = RuleText.Problem(rule, AccountNames);

        // Assert
        await Assert.That(problem).IsEqualTo("Looks at an account you no longer have, so it can't match");
    }

    [Test]
    public async Task Problem_RuleThatCanRun_IsNull() =>
        await Assert.That(RuleText.Problem(RuleWith(RuleMatchMode.All,
            [new RuleAction { Type = RuleActionType.MarkAsTransfer }]), AccountNames)).IsNull();

    [Test]
    public async Task OperatorsFor_EachField_OnlyTheOnesTheServerAccepts()
    {
        using (Assert.Multiple())
        {
            await Assert.That(RuleText.OperatorsFor(RuleField.Amount)).DoesNotContain(RuleOperator.Contains);
            await Assert.That(RuleText.OperatorsFor(RuleField.Description)).DoesNotContain(RuleOperator.Between);
            await Assert.That(RuleText.OperatorsFor(RuleField.Account)).IsEquivalentTo([RuleOperator.Equals]);
        }
    }

    [Test]
    public async Task Changes_ListsTagsGainedAndLostThenTheTransferAnswer()
    {
        // Arrange
        RulePreviewChange change = new()
        {
            Description = "GAME STORE",
            TagsAdded = ["Gaming"],
            TagsRemoved = ["Shopping"],
            ClassificationsHidden = ["TV"],
            TransferBefore = null,
            TransferAfter = TransferState.NotATransfer
        };

        // Act
        List<string> changes = RuleText.Changes(change).ToList();

        // Assert
        await Assert.That(changes).IsEquivalentTo(
            ["+ Gaming", "− Shopping", "Hides TV", "Counts as income or spending"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task TaggedBy_NamesTheRuleOnlyForTagsARuleAdded()
    {
        // Arrange
        Transaction transaction = new()
        {
            TransactionId = Guid.NewGuid(),
            Description = "GAME STORE",
            TransactionType = "Debit",
            TransactionCategory = "PURCHASE",
            Amount = -20m,
            Currency = "GBP",
            TransactionTime = DateTime.UtcNow,
            Pending = false,
            Tags = ["Gaming", "Mine"],
            TagsFromRules = [new TransactionRuleTag { Tag = "Gaming", RuleId = Guid.NewGuid(), RuleName = "Games" }]
        };

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(RuleText.TaggedBy(transaction, "Gaming")).IsEqualTo("Games");
            await Assert.That(RuleText.TaggedBy(transaction, "Mine")).IsNull();
            await Assert.That(RuleText.TaggedByLines(transaction)).IsEquivalentTo(["Gaming from your rule Games"]);
        }
    }

    private static RuleCondition Condition(RuleField field, RuleOperator @operator, string value,
        string? secondValue = null) =>
        new() { Field = field, Operator = @operator, Value = value, SecondValue = secondValue };

    private static Rule RuleWith(RuleMatchMode matchMode, List<RuleAction> actions) => new()
    {
        Name = "Games",
        MatchMode = matchMode,
        Conditions =
        [
            Condition(RuleField.Counterparty, RuleOperator.Contains, "a"),
            Condition(RuleField.Description, RuleOperator.Contains, "b")
        ],
        Actions = actions
    };
}
