using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Tests.Unit.Domain.OpenBanking.Transactions;

[Category("Domain")]
public class BankTransactionBillPaymentTests
{
    private static readonly DateTime At = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly BankAccount Account = new(UserId, Guid.NewGuid(), "acc-1", "TRANSACTION", "Main", "GBP");

    [Test]
    [Arguments(DecisionSource.Matcher, DecisionSource.Matcher, true)]
    [Arguments(DecisionSource.Matcher, DecisionSource.Rule, true)]
    [Arguments(DecisionSource.Rule, DecisionSource.User, true)]
    [Arguments(DecisionSource.Rule, DecisionSource.Matcher, false)]
    [Arguments(DecisionSource.User, DecisionSource.Rule, false)]
    public async Task DecideBillPayment_OnlyReplacesAnEqualOrLowerSource(DecisionSource existing,
        DecisionSource incoming, bool replaced)
    {
        // Arrange
        BankTransaction transaction = Build();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        transaction.DecideBillPayment(new BillPayment(first, existing, At));

        // Act
        bool recorded = transaction.DecideBillPayment(new BillPayment(second, incoming, At));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(recorded).IsEqualTo(replaced);
            await Assert.That(transaction.PaysBill(second)).IsEqualTo(replaced);
            await Assert.That(transaction.PaysBill(first)).IsEqualTo(!replaced);
        }
    }

    [Test]
    [Arguments(DecisionSource.Matcher, DecisionSource.Rule, true)]
    [Arguments(DecisionSource.User, DecisionSource.Rule, false)]
    public async Task ClearBillPayment_OnlyAnEqualOrLowerSource(DecisionSource existing, DecisionSource clearing,
        bool cleared)
    {
        // Arrange
        BankTransaction transaction = Build();
        transaction.DecideBillPayment(new BillPayment(Guid.NewGuid(), existing, At));

        // Act
        bool removed = transaction.ClearBillPayment(clearing);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(removed).IsEqualTo(cleared);
            await Assert.That(transaction.BillPayment is null).IsEqualTo(cleared);
        }
    }

    [Test]
    public async Task PayBill_ByTheUser_MatchingCantReplaceIt()
    {
        // Arrange
        BankTransaction transaction = Build();
        Bill bill = Bill(UserId);
        transaction.PayBill(bill, transaction.UserId, At);

        // Act
        bool replaced = transaction.DecideBillPayment(BillPayment.Matched(Guid.NewGuid(), At));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(replaced).IsFalse();
            await Assert.That(transaction.PaysBill(bill.Id)).IsTrue();
            await Assert.That(transaction.BillPayment!.Source).IsEqualTo(DecisionSource.User);
        }
    }

    [Test]
    public async Task PayBill_AnotherUsersBill_Throws()
    {
        BankTransaction transaction = Build();
        await Assert.That(() => transaction.PayBill(Bill(Guid.NewGuid()), transaction.UserId, At))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task PayNoBill_ByTheUser_PaysNoneAndMatchingCantLinkIt()
    {
        // Arrange
        BankTransaction transaction = Build();
        Guid billId = Guid.NewGuid();
        transaction.DecideBillPayment(BillPayment.Matched(billId, At));

        // Act
        transaction.PayNoBill(At);
        bool relinked = transaction.DecideBillPayment(BillPayment.Matched(billId, At));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(relinked).IsFalse();
            await Assert.That(transaction.PaysBill(billId)).IsFalse();
            await Assert.That(transaction.BillPayment!.BillId).IsNull();
        }
    }

    [Test]
    public async Task RemoveDeletedBill_EvenTheUsersPayment_IsDropped()
    {
        // Arrange
        BankTransaction transaction = Build();
        Bill bill = Bill(UserId);
        transaction.PayBill(bill, transaction.UserId, At);

        // Act
        bool removed = transaction.RemoveDeletedBill(bill.Id);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(removed).IsTrue();
            await Assert.That(transaction.BillPayment).IsNull();
        }
    }

    [Test]
    public async Task RemoveDeletedBill_AnotherBill_LeavesItAlone()
    {
        // Arrange
        BankTransaction transaction = Build();
        Bill bill = Bill(UserId);
        transaction.PayBill(bill, transaction.UserId, At);

        // Act
        bool removed = transaction.RemoveDeletedBill(Guid.NewGuid());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(removed).IsFalse();
            await Assert.That(transaction.PaysBill(bill.Id)).IsTrue();
        }
    }

    private static Bill Bill(Guid userId) =>
        new(userId, Account.Id, "Streaming", "Streamco", BillFrequency.Monthly, 9.99m, new DateOnly(2026, 1, 1),
            BillSource.Manual);

    private static BankTransaction Build() =>
        BankTransaction.Import(Account, new TransactionDetails("tx-1", "Streamco", TransactionType.Debit, "PURCHASE",
            -9.99m, "GBP", At, false, [], TransactionMetadata.None, false));
}
