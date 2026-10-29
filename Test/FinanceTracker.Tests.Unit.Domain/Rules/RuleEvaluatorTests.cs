using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Domain.Rules;

namespace FinanceTracker.Tests.Unit.Domain.Rules;

[Category("Domain")]
public class RuleEvaluatorTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime At = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly BankAccount Account = new(UserId, Guid.NewGuid(), "acc-1", "TRANSACTION", "Main", "GBP");

    [Test]
    public async Task Evaluate_EveryMatchingRulesTags_Stack()
    {
        CustomClassification entertainment = Tag("Entertainment");
        CustomClassification gaming = Tag("Gaming");
        Rule first = Rule("Entertainment", 0, DescriptionContains("GAME STORE"),
            RuleAction.AddTag(entertainment));
        Rule second = Rule("Gaming", 1, DescriptionContains("GAME"), RuleAction.AddTag(gaming));

        RuleOutcome outcome = RuleEvaluator.Evaluate([second, first], Transaction("GAME STORE PURCHASE"));

        await Assert.That(outcome.Tags).IsEquivalentTo([
            new RuleTag(entertainment.Id, first.Id),
            new RuleTag(gaming.Id, second.Id)
        ]);
    }

    [Test]
    public async Task Evaluate_SameTagFromTwoRules_CreditsTheFirstInOrder()
    {
        CustomClassification shopping = Tag("Shopping");
        Rule later = Rule("Later", 5, DescriptionContains("GROCER"), RuleAction.AddTag(shopping));
        Rule earlier = Rule("Earlier", 1, DescriptionContains("STORE"), RuleAction.AddTag(shopping));

        RuleOutcome outcome = RuleEvaluator.Evaluate([later, earlier], Transaction("GROCER STORE 123"));

        await Assert.That(outcome.Tags).IsEquivalentTo([new RuleTag(shopping.Id, earlier.Id)]);
    }

    [Test]
    public async Task Evaluate_TransferAnswer_FirstMatchingRuleWins()
    {
        Rule notATransfer = Rule("Not", 0, DescriptionContains("STORE"),
            RuleAction.MarkNotATransfer());
        Rule transfer = Rule("Is", 1, DescriptionContains("GROCER"), RuleAction.MarkAsTransfer());

        RuleOutcome outcome = RuleEvaluator.Evaluate([transfer, notATransfer], Transaction("GROCER STORE 123"));

        await Assert.That(outcome.Transfer).IsEqualTo(new RuleTransfer(TransferDecision.NotATransfer,
            notATransfer.Id));
    }

    [Test]
    public async Task Evaluate_StopProcessing_SkipsLaterRules()
    {
        CustomClassification shopping = Tag("Shopping");
        CustomClassification food = Tag("Food");
        Rule stopping = Rule("Stop", 0, DescriptionContains("GROCER"), RuleAction.AddTag(shopping));
        stopping.StopProcessingAfterMatch(true);
        Rule skipped = Rule("Skipped", 1, DescriptionContains("GROCER"), RuleAction.AddTag(food));

        RuleOutcome outcome = RuleEvaluator.Evaluate([stopping, skipped], Transaction("GROCER STORE 123"));

        await Assert.That(outcome.Tags).IsEquivalentTo([new RuleTag(shopping.Id, stopping.Id)]);
    }

    [Test]
    public async Task Evaluate_StopProcessingRuleThatDoesNotMatch_DoesNotStop()
    {
        CustomClassification food = Tag("Food");
        Rule stopping = Rule("Stop", 0, DescriptionContains("CAFE"), RuleAction.MarkAsTransfer());
        stopping.StopProcessingAfterMatch(true);
        Rule later = Rule("Later", 1, DescriptionContains("GROCER"), RuleAction.AddTag(food));

        RuleOutcome outcome = RuleEvaluator.Evaluate([stopping, later], Transaction("GROCER STORE 123"));

        await Assert.That(outcome.Tags).IsEquivalentTo([new RuleTag(food.Id, later.Id)]);
    }

    [Test]
    public async Task Evaluate_DisabledRule_IsSkipped()
    {
        Rule rule = Rule("Off", 0, DescriptionContains("GROCER"), RuleAction.MarkAsTransfer());
        rule.Disable();

        RuleOutcome outcome = RuleEvaluator.Evaluate([rule], Transaction("GROCER STORE 123"));

        using (Assert.Multiple())
        {
            await Assert.That(outcome.Tags).IsEmpty();
            await Assert.That(outcome.Transfer).IsNull();
        }
    }

    [Test]
    public async Task Evaluate_DoesNotChangeTheTransaction()
    {
        Rule rule = Rule("Transfer", 0, DescriptionContains("GROCER"), RuleAction.MarkAsTransfer(),
            RuleAction.AddTag(Tag("Shopping")));
        BankTransaction transaction = Transaction("GROCER STORE 123");

        RuleEvaluator.Evaluate([rule], transaction);

        using (Assert.Multiple())
        {
            await Assert.That(transaction.Transfer).IsNull();
            await Assert.That(transaction.CustomClassificationIds).IsEmpty();
        }
    }

    [Test]
    public void Evaluate_AnotherUsersRule_Throws()
    {
        Rule theirs = new(Guid.NewGuid(), "Theirs", 0, RuleMatchMode.All, [DescriptionContains("GROCER")],
            [RuleAction.MarkAsTransfer()]);

        Assert.Throws<InvalidOperationException>(() =>
            RuleEvaluator.Evaluate([theirs], Transaction("GROCER STORE 123")));
    }

    [Test]
    public async Task Apply_RecordsTheOutcome()
    {
        CustomClassification savings = Tag("Savings");
        Rule rule = Rule("Investments", 0, DescriptionContains("INVEST CO"),
            RuleAction.MarkAsTransfer(), RuleAction.AddTag(savings));
        BankTransaction transaction = Transaction("INVEST CO 0001");

        bool changed = RuleEvaluator.Apply([rule], transaction, At);

        using (Assert.Multiple())
        {
            await Assert.That(changed).IsTrue();
            await Assert.That(transaction.Transfer)
                .IsEqualTo(TransferInfo.FromRule(TransferDecision.Transfer, rule.Id, At));
            await Assert.That(transaction.CustomClassificationIds).IsEquivalentTo([savings.Id]);
            await Assert.That(transaction.RuleTags).IsEquivalentTo([new RuleTag(savings.Id, rule.Id)]);
            await Assert.That(transaction.CountsAsIncomeOrSpending).IsFalse();
        }
    }

    [Test]
    public async Task Apply_Again_ChangesNothing()
    {
        Rule rule = Rule("Investments", 0, DescriptionContains("INVEST CO"),
            RuleAction.MarkAsTransfer(), RuleAction.AddTag(Tag("Savings")));
        BankTransaction transaction = Transaction("INVEST CO 0001");
        RuleEvaluator.Apply([rule], transaction, At);

        bool changed = RuleEvaluator.Apply([rule], transaction, At.AddDays(1));

        using (Assert.Multiple())
        {
            await Assert.That(changed).IsFalse();
            await Assert.That(transaction.Transfer!.DecidedAt).IsEqualTo(At);
        }
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
