using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Domain.Rules;

namespace FinanceTracker.Tests.Unit.Domain.Rules;

/// <summary>
/// How a transaction records what rules decided, and how that sits with the user's and the matcher's decisions.
/// </summary>
[Category("Domain")]
public class BankTransactionRuleTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime At = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly BankAccount Account = new(UserId, Guid.NewGuid(), "acc-1", "TRANSACTION", "Main", "GBP");

    [Test]
    public async Task ApplyRules_RuleNoLongerGivesATag_RemovesIt()
    {
        CustomClassification shopping = Tag("Shopping");
        CustomClassification food = Tag("Food");
        Guid ruleId = Guid.NewGuid();
        BankTransaction transaction = Transaction();
        transaction.ApplyRules(new RuleOutcome([new RuleTag(shopping.Id, ruleId), new RuleTag(food.Id, ruleId)], null),
            At);

        bool changed = transaction.ApplyRules(new RuleOutcome([new RuleTag(food.Id, ruleId)], null), At);

        using (Assert.Multiple())
        {
            await Assert.That(changed).IsTrue();
            await Assert.That(transaction.CustomClassificationIds).IsEquivalentTo([food.Id]);
            await Assert.That(transaction.RuleTags).IsEquivalentTo([new RuleTag(food.Id, ruleId)]);
        }
    }

    [Test]
    public async Task ApplyRules_TagsSetByUser_LeavesTagsAlone()
    {
        CustomClassification shopping = Tag("Shopping");
        CustomClassification food = Tag("Food");
        BankTransaction transaction = Transaction();
        transaction.Classify(food);

        bool changed = transaction.ApplyRules(new RuleOutcome([new RuleTag(shopping.Id, Guid.NewGuid())], null), At);

        using (Assert.Multiple())
        {
            await Assert.That(changed).IsFalse();
            await Assert.That(transaction.CustomClassificationIds).IsEquivalentTo([food.Id]);
            await Assert.That(transaction.RuleTags).IsEmpty();
        }
    }

    [Test]
    public async Task ApplyRules_TagTheUserRemoved_IsNotAddedBack()
    {
        CustomClassification shopping = Tag("Shopping");
        RuleOutcome outcome = new([new RuleTag(shopping.Id, Guid.NewGuid())], null);
        BankTransaction transaction = Transaction();
        transaction.ApplyRules(outcome, At);
        transaction.Unclassify(shopping.Id);

        transaction.ApplyRules(outcome, At);

        using (Assert.Multiple())
        {
            await Assert.That(transaction.TagsSetByUser).IsTrue();
            await Assert.That(transaction.CustomClassificationIds).IsEmpty();
            await Assert.That(transaction.RuleTags).IsEmpty();
        }
    }

    [Test]
    public async Task Classify_TagARuleAdded_BecomesTheUsers()
    {
        CustomClassification shopping = Tag("Shopping");
        BankTransaction transaction = Transaction();
        transaction.ApplyRules(new RuleOutcome([new RuleTag(shopping.Id, Guid.NewGuid())], null), At);

        transaction.Classify(shopping);

        using (Assert.Multiple())
        {
            await Assert.That(transaction.CustomClassificationIds).IsEquivalentTo([shopping.Id]);
            await Assert.That(transaction.RuleTags).IsEmpty();
            await Assert.That(transaction.TagsSetByUser).IsTrue();
        }
    }

    [Test]
    public async Task RemoveDeletedClassification_DoesNotCountAsTheUsersEdit()
    {
        CustomClassification shopping = Tag("Shopping");
        BankTransaction transaction = Transaction();
        transaction.ApplyRules(new RuleOutcome([new RuleTag(shopping.Id, Guid.NewGuid())], null), At);

        transaction.RemoveDeletedClassification(shopping.Id);

        using (Assert.Multiple())
        {
            await Assert.That(transaction.CustomClassificationIds).IsEmpty();
            await Assert.That(transaction.RuleTags).IsEmpty();
            await Assert.That(transaction.TagsSetByUser).IsFalse();
        }
    }

    [Test]
    public async Task ApplyRules_ReplacesTheMatchersDecision()
    {
        Guid ruleId = Guid.NewGuid();
        BankTransaction transaction = Transaction();
        transaction.DecideTransfer(TransferInfo.ToReview(TransferReason.BankTransferLabel, null, null, At));

        transaction.ApplyRules(new RuleOutcome([], new RuleTransfer(TransferDecision.NotATransfer, ruleId)), At);

        await Assert.That(transaction.Transfer)
            .IsEqualTo(TransferInfo.FromRule(TransferDecision.NotATransfer, ruleId, At));
    }

    [Test]
    public async Task ApplyRules_NeverReplacesTheUsersDecision()
    {
        BankTransaction transaction = Transaction();
        transaction.MarkNotATransfer(At);

        bool changed = transaction.ApplyRules(
            new RuleOutcome([], new RuleTransfer(TransferDecision.Transfer, Guid.NewGuid())), At);

        using (Assert.Multiple())
        {
            await Assert.That(changed).IsFalse();
            await Assert.That(transaction.Transfer!.Source).IsEqualTo(DecisionSource.User);
        }
    }

    [Test]
    public async Task ApplyRules_RuleNoLongerGivesATransferAnswer_TakesItBack()
    {
        BankTransaction transaction = Transaction();
        transaction.ApplyRules(new RuleOutcome([], new RuleTransfer(TransferDecision.Transfer, Guid.NewGuid())), At);

        bool changed = transaction.ApplyRules(RuleOutcome.None, At);

        using (Assert.Multiple())
        {
            await Assert.That(changed).IsTrue();
            await Assert.That(transaction.Transfer).IsNull();
        }
    }

    [Test]
    public async Task ApplyRules_NoTransferAnswer_LeavesTheMatchersDecision()
    {
        BankTransaction transaction = Transaction();
        TransferInfo matched = TransferInfo.ToReview(TransferReason.BankTransferLabel, null, null, At);
        transaction.DecideTransfer(matched);

        bool changed = transaction.ApplyRules(RuleOutcome.None, At);

        using (Assert.Multiple())
        {
            await Assert.That(changed).IsFalse();
            await Assert.That(transaction.Transfer).IsEqualTo(matched);
        }
    }

    private static BankTransaction Transaction(string description = "GROCER STORE 123", decimal amount = -12.5m,
        TransactionMetadata? metadata = null, IReadOnlyCollection<string>? providerClassifications = null) =>
        BankTransaction.Import(Account, new TransactionDetails(Guid.NewGuid().ToString(), description,
            amount < 0 ? TransactionType.Debit : TransactionType.Credit, "PURCHASE", amount, "GBP", At, false,
            providerClassifications ?? ["Shopping"], metadata ?? TransactionMetadata.None, false));

    private static CustomClassification Tag(string name) => new(UserId, name);
}
