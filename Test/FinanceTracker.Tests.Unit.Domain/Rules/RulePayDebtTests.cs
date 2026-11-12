using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Debts;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Rules;

namespace FinanceTracker.Tests.Unit.Domain.Rules;

[Category("Domain")]
public class RulePayDebtTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime At = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly BankAccount Account = new(UserId, Guid.NewGuid(), "acc-1", "TRANSACTION", "Main", "GBP");

    [Test]
    public async Task Constructor_TwoDebts_Throws() =>
        await Assert.That(() => Rule("Both", 0, RuleAction.PayDebt(Debt()), RuleAction.PayDebt(Debt())))
            .Throws<ArgumentException>();

    [Test]
    public async Task Constructor_PayDebtWithoutADebt_Throws() =>
        await Assert.That(() => Rule("None", 0, new RuleAction(RuleActionType.PayDebt, null)))
            .Throws<ArgumentException>();

    [Test]
    public async Task RemoveDebt_ItsOnlyAction_DisablesTheRule()
    {
        // Arrange
        Debt debt = Debt();
        Rule rule = Rule("Car finance", 0, RuleAction.PayDebt(debt));

        // Act
        bool removed = rule.RemoveDebt(debt.Id);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(removed).IsTrue();
            await Assert.That(rule.Actions).IsEmpty();
            await Assert.That(rule.IsEnabled).IsFalse();
        }
    }

    [Test]
    public async Task Evaluate_Debt_FirstMatchingRuleWins()
    {
        // Arrange
        Debt first = Debt();
        Rule earlier = Rule("Earlier", 0, RuleAction.PayDebt(first));
        Rule later = Rule("Later", 1, RuleAction.PayDebt(Debt()));

        // Act
        RuleOutcome outcome = RuleEvaluator.Evaluate([later, earlier], Transaction());

        // Assert
        await Assert.That(outcome.Debt).IsEqualTo(new RuleDebt(first.Id, earlier.Id));
    }

    [Test]
    public async Task Apply_PaysABillAndADebtAtOnce()
    {
        // Arrange
        Bill bill = new(UserId, Account.Id, "Car finance", "Car Finance Co", BillFrequency.Monthly, 199m,
            new DateOnly(2026, 1, 1), BillSource.Manual);
        Debt debt = Debt();
        Rule rule = Rule("Car finance", 0, RuleAction.PayBill(bill), RuleAction.PayDebt(debt));
        BankTransaction transaction = Transaction();

        // Act
        RuleEvaluator.Apply([rule], transaction, At);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(transaction.PaysBill(bill.Id)).IsTrue();
            await Assert.That(transaction.PaysDebt(debt.Id)).IsTrue();
            await Assert.That(transaction.DebtPayment!.Source).IsEqualTo(DecisionSource.Rule);
            await Assert.That(transaction.WasDecidedBy(rule.Id)).IsTrue();
        }
    }

    [Test]
    public async Task Apply_RuleNoLongerMatches_TakesTheDebtBack()
    {
        // Arrange
        Debt debt = Debt();
        Rule rule = Rule("Car finance", 0, RuleAction.PayDebt(debt));
        BankTransaction transaction = Transaction();
        RuleEvaluator.Apply([rule], transaction, At);

        // Act
        rule.Disable();
        bool changed = RuleEvaluator.Apply([rule], transaction, At);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(changed).IsTrue();
            await Assert.That(transaction.DebtPayment).IsNull();
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Apply_TheUsersAnswer_IsLeftAlone(bool paysADebt)
    {
        // Arrange
        Debt usersDebt = Debt();
        Rule rule = Rule("Car finance", 0, RuleAction.PayDebt(Debt()));
        BankTransaction transaction = Transaction();
        if (paysADebt)
        {
            transaction.PayDebt(usersDebt, transaction.UserId, At);
        }
        else
        {
            transaction.PayNoDebt(At);
        }

        // Act
        bool changed = RuleEvaluator.Apply([rule], transaction, At);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(changed).IsFalse();
            await Assert.That(transaction.DebtPayment!.Source).IsEqualTo(DecisionSource.User);
            await Assert.That(transaction.DebtPayment!.DebtId).IsEqualTo(paysADebt ? usersDebt.Id : null);
        }
    }

    [Test]
    public async Task Apply_AgainUnchanged_ChangesNothing()
    {
        // Arrange
        Rule rule = Rule("Car finance", 0, RuleAction.PayDebt(Debt()));
        BankTransaction transaction = Transaction();
        RuleEvaluator.Apply([rule], transaction, At);

        // Act
        bool changed = RuleEvaluator.Apply([rule], transaction, At.AddHours(1));

        // Assert
        await Assert.That(changed).IsFalse();
    }

    [Test]
    public async Task PayDebt_AnotherUsersDebt_Throws()
    {
        BankTransaction transaction = Transaction();
        await Assert.That(() => transaction.PayDebt(
                new Debt(Guid.NewGuid(), "Loan", null, 1000m, new DateOnly(2026, 1, 1), 0m, 100m), transaction.UserId,
                At))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task RemoveDeletedDebt_OnlyThatDebtsPayment_IsDropped()
    {
        // Arrange
        Debt deleted = Debt();
        Debt kept = Debt();
        BankTransaction paysDeleted = Transaction();
        paysDeleted.PayDebt(deleted, paysDeleted.UserId, At);
        BankTransaction paysKept = Transaction();
        paysKept.PayDebt(kept, paysKept.UserId, At);

        // Act
        bool removed = paysDeleted.RemoveDeletedDebt(deleted.Id);
        bool removedOther = paysKept.RemoveDeletedDebt(deleted.Id);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(removed).IsTrue();
            await Assert.That(paysDeleted.DebtPayment).IsNull();
            await Assert.That(removedOther).IsFalse();
            await Assert.That(paysKept.PaysDebt(kept.Id)).IsTrue();
        }
    }

    private static Debt Debt() => new(UserId, "Car loan", "Car Finance Co", 5000m, new DateOnly(2026, 1, 1), 9.9m, 199m);

    private static BankTransaction Transaction() =>
        BankTransaction.Import(Account, new TransactionDetails(Guid.NewGuid().ToString(), "CAR FINANCE CO",
            TransactionType.Debit, "PURCHASE", -199m, "GBP", At, false, [], TransactionMetadata.None, false));

    private static Rule Rule(string name, int order, params RuleAction[] actions) =>
        new(UserId, name, order, RuleMatchMode.All, [new RuleCondition(RuleField.Description, RuleOperator.Contains,
            "CAR FINANCE")], actions);
}
