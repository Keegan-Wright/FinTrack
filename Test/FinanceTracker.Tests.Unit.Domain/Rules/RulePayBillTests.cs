using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Rules;

namespace FinanceTracker.Tests.Unit.Domain.Rules;

[Category("Domain")]
public class RulePayBillTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime At = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly BankAccount Account = new(UserId, Guid.NewGuid(), "acc-1", "TRANSACTION", "Main", "GBP");

    [Test]
    public async Task Constructor_TwoBills_Throws() =>
        await Assert.That(() => Rule("Both", 0, RuleAction.PayBill(Bill()), RuleAction.PayBill(Bill())))
            .Throws<ArgumentException>();

    [Test]
    public async Task Constructor_PayBillWithoutABill_Throws() =>
        await Assert.That(() => Rule("None", 0, new RuleAction(RuleActionType.PayBill, null)))
            .Throws<ArgumentException>();

    [Test]
    public async Task RemoveBill_ItsOnlyAction_DisablesTheRule()
    {
        // Arrange
        Bill bill = Bill();
        Rule rule = Rule("Streaming", 0, RuleAction.PayBill(bill));

        // Act
        bool removed = rule.RemoveBill(bill.Id);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(removed).IsTrue();
            await Assert.That(rule.Actions).IsEmpty();
            await Assert.That(rule.IsEnabled).IsFalse();
        }
    }

    [Test]
    public async Task Evaluate_Bill_FirstMatchingRuleWins()
    {
        // Arrange
        Bill first = Bill();
        Bill second = Bill();
        Rule later = Rule("Later", 1, RuleAction.PayBill(second));
        Rule earlier = Rule("Earlier", 0, RuleAction.PayBill(first));

        // Act
        RuleOutcome outcome = RuleEvaluator.Evaluate([later, earlier], Transaction());

        // Assert
        await Assert.That(outcome.Bill).IsEqualTo(new RuleBill(first.Id, earlier.Id));
    }

    [Test]
    public async Task Apply_PaysTheBillAndTakesItBackOnceTheRuleNoLongerMatches()
    {
        // Arrange
        Bill bill = Bill();
        Rule rule = Rule("Streaming", 0, RuleAction.PayBill(bill));
        BankTransaction transaction = Transaction();
        RuleEvaluator.Apply([rule], transaction, At);
        bool paidBefore = transaction.PaysBill(bill.Id);
        bool decidedByRule = transaction.WasDecidedBy(rule.Id);

        // Act
        rule.Disable();
        bool changed = RuleEvaluator.Apply([rule], transaction, At);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(paidBefore).IsTrue();
            await Assert.That(decidedByRule).IsTrue();
            await Assert.That(changed).IsTrue();
            await Assert.That(transaction.BillPayment).IsNull();
        }
    }

    [Test]
    public async Task Apply_UsersBill_IsLeftAlone()
    {
        // Arrange
        Bill usersBill = Bill();
        Rule rule = Rule("Streaming", 0, RuleAction.PayBill(Bill()));
        BankTransaction transaction = Transaction();
        transaction.PayBill(usersBill, transaction.UserId, At);

        // Act
        bool changed = RuleEvaluator.Apply([rule], transaction, At);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(changed).IsFalse();
            await Assert.That(transaction.PaysBill(usersBill.Id)).IsTrue();
            await Assert.That(transaction.BillPayment!.Source).IsEqualTo(DecisionSource.User);
        }
    }

    [Test]
    public async Task Apply_AgainUnchanged_ChangesNothing()
    {
        // Arrange
        Rule rule = Rule("Streaming", 0, RuleAction.PayBill(Bill()));
        BankTransaction transaction = Transaction();
        RuleEvaluator.Apply([rule], transaction, At);

        // Act
        bool changed = RuleEvaluator.Apply([rule], transaction, At.AddHours(1));

        // Assert
        await Assert.That(changed).IsFalse();
    }

    private static Bill Bill() =>
        new(UserId, Account.Id, "Streaming", "Streamco", BillFrequency.Monthly, 9.99m, new DateOnly(2026, 1, 1),
            BillSource.Manual);

    private static BankTransaction Transaction() =>
        BankTransaction.Import(Account, new TransactionDetails(Guid.NewGuid().ToString(), "STREAMCO 123",
            TransactionType.Debit, "PURCHASE", -9.99m, "GBP", At, false, [], TransactionMetadata.None, false));

    private static Rule Rule(string name, int order, params RuleAction[] actions) =>
        new(UserId, name, order, RuleMatchMode.All, [new RuleCondition(RuleField.Description, RuleOperator.Contains,
            "STREAMCO")], actions);
}
