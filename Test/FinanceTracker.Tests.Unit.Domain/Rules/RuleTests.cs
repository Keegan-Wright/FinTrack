using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Rules;

namespace FinanceTracker.Tests.Unit.Domain.Rules;

[Category("Domain")]
public class RuleTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime At = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly BankAccount Account = new(UserId, Guid.NewGuid(), "acc-1", "TRANSACTION", "Main", "GBP");

    [Test]
    public async Task New_SetsItsDetailsAndIsEnabled()
    {
        CustomClassification shopping = Tag("Shopping");
        RuleCondition condition = DescriptionContains("GROCER");

        Rule rule = new(UserId, "Groceries", 3, RuleMatchMode.Any, [condition], [RuleAction.AddTag(shopping)]);

        using (Assert.Multiple())
        {
            await Assert.That(rule.UserId).IsEqualTo(UserId);
            await Assert.That(rule.Name).IsEqualTo("Groceries");
            await Assert.That(rule.Order).IsEqualTo(3);
            await Assert.That(rule.MatchMode).IsEqualTo(RuleMatchMode.Any);
            await Assert.That(rule.IsEnabled).IsTrue();
            await Assert.That(rule.StopProcessing).IsFalse();
            await Assert.That(rule.Conditions).IsEquivalentTo([condition]);
            await Assert.That(rule.Actions).IsEquivalentTo([RuleAction.AddTag(shopping)]);
        }
    }

    [Test]
    public void New_NoConditions_Throws() =>
        Assert.Throws<ArgumentException>(() => _ = new Rule(UserId, "Empty", 0, RuleMatchMode.All, [],
            [RuleAction.MarkAsTransfer()]));

    [Test]
    public void New_NoActions_Throws() =>
        Assert.Throws<ArgumentException>(() => _ = new Rule(UserId, "Empty", 0, RuleMatchMode.All,
            [DescriptionContains("GROCER")], []));

    [Test]
    public void New_TwoTransferAnswers_Throws() =>
        Assert.Throws<ArgumentException>(() => Rule("Both", 0, DescriptionContains("GROCER"),
            RuleAction.MarkAsTransfer(), RuleAction.MarkNotATransfer()));

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    public void New_BlankName_Throws(string name) =>
        Assert.Throws<ArgumentException>(() => Rule(name, 0, DescriptionContains("GROCER"),
            RuleAction.MarkAsTransfer()));

    [Test]
    public void MoveTo_Negative_Throws()
    {
        Rule rule = Rule("Groceries", 0, DescriptionContains("GROCER"), RuleAction.MarkAsTransfer());

        Assert.Throws<ArgumentOutOfRangeException>(() => rule.MoveTo(-1));
    }

    [Test]
    public async Task ReplaceActions_SameTagTwice_KeepsItOnce()
    {
        CustomClassification shopping = Tag("Shopping");
        Rule rule = Rule("Groceries", 0, DescriptionContains("GROCER"), RuleAction.MarkAsTransfer());

        rule.ReplaceActions([RuleAction.AddTag(shopping), RuleAction.AddTag(shopping)]);

        await Assert.That(rule.Actions).IsEquivalentTo([RuleAction.AddTag(shopping)]);
    }

    [Test]
    public async Task EnableAndDisable()
    {
        Rule rule = Rule("Groceries", 0, DescriptionContains("GROCER"), RuleAction.MarkAsTransfer());

        rule.Disable();
        bool disabled = !rule.IsEnabled;
        rule.Enable();

        using (Assert.Multiple())
        {
            await Assert.That(disabled).IsTrue();
            await Assert.That(rule.IsEnabled).IsTrue();
        }
    }

    [Test]
    [Arguments(RuleMatchMode.All, false)]
    [Arguments(RuleMatchMode.Any, true)]
    public async Task Matches_ByMatchMode(RuleMatchMode matchMode, bool matches)
    {
        Rule rule = new(UserId, "Groceries", 0, matchMode,
            [DescriptionContains("GROCER"), DescriptionContains("CAFE")],
            [RuleAction.MarkAsTransfer()]);

        await Assert.That(rule.Matches(Transaction("GROCER STORE 123"))).IsEqualTo(matches);
    }

    [Test]
    public async Task Matches_Disabled_StillMatchesForAPreview()
    {
        Rule rule = Rule("Groceries", 0, DescriptionContains("GROCER"), RuleAction.MarkAsTransfer());
        rule.Disable();

        await Assert.That(rule.Matches(Transaction("GROCER STORE 123"))).IsTrue();
    }

    [Test]
    public async Task RemoveTag_DropsOnlyThatTagsAction()
    {
        // Arrange
        CustomClassification shopping = Tag("Shopping");
        CustomClassification food = Tag("Food");
        Rule rule = Rule("Groceries", 0, DescriptionContains("GROCER"), RuleAction.AddTag(shopping),
            RuleAction.AddTag(food));

        // Act
        bool removed = rule.RemoveTag(shopping.Id);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(removed).IsTrue();
            await Assert.That(rule.Actions).IsEquivalentTo([RuleAction.AddTag(food)]);
            await Assert.That(rule.IsEnabled).IsTrue();
        }
    }

    [Test]
    public async Task RemoveTag_LastAction_DisablesTheRule()
    {
        // Arrange
        CustomClassification shopping = Tag("Shopping");
        Rule rule = Rule("Groceries", 0, DescriptionContains("GROCER"), RuleAction.AddTag(shopping));

        // Act
        rule.RemoveTag(shopping.Id);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(rule.Actions).IsEmpty();
            await Assert.That(rule.IsEnabled).IsFalse();
        }
    }

    [Test]
    public async Task RemoveTag_TagTheRuleDoesntAdd_ChangesNothing()
    {
        // Arrange
        Rule rule = Rule("Groceries", 0, DescriptionContains("GROCER"), RuleAction.MarkAsTransfer());

        // Act
        bool removed = rule.RemoveTag(Guid.NewGuid());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(removed).IsFalse();
            await Assert.That(rule.Actions).IsEquivalentTo([RuleAction.MarkAsTransfer()]);
        }
    }

    [Test]
    public void Enable_NoActionsLeft_Throws()
    {
        CustomClassification shopping = Tag("Shopping");
        Rule rule = Rule("Groceries", 0, DescriptionContains("GROCER"), RuleAction.AddTag(shopping));
        rule.RemoveTag(shopping.Id);

        Assert.Throws<InvalidOperationException>(rule.Enable);
    }

    private static BankTransaction Transaction(string description = "GROCER STORE 123", decimal amount = -12.5m,
        TransactionMetadata? metadata = null, IReadOnlyCollection<string>? providerClassifications = null) =>
        BankTransaction.Import(Account, new TransactionDetails(Guid.NewGuid().ToString(), description,
            amount < 0 ? TransactionType.Debit : TransactionType.Credit, "PURCHASE", amount, "GBP", At, false,
            providerClassifications ?? ["Shopping"], metadata ?? TransactionMetadata.None, false));

    private static CustomClassification Tag(string name) => new(UserId, name);

    private static Rule Rule(string name, int order, RuleCondition condition, params RuleAction[] actions) =>
        new(UserId, name, order, RuleMatchMode.All, [condition], actions);

    private static RuleCondition DescriptionContains(string value) =>
        new(RuleField.Description, RuleOperator.Contains, value);
}
