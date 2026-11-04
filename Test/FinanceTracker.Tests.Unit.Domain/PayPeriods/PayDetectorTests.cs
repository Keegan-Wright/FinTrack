using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.PayPeriods;

namespace FinanceTracker.Tests.Unit.Domain.PayPeriods;

[Category("Domain")]
public class PayDetectorTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly BankAccount Account = new(UserId, Guid.NewGuid(), "acc-1", "TRANSACTION", "Main", "GBP");
    private static readonly DateOnly Today = new(2026, 11, 4);

    [Test]
    public async Task Detect_ThreeMonthlyPays_IsThePayOnTheLastWorkingDay()
    {
        // Arrange
        List<BankTransaction> transactions =
        [
            Credit("ACME LTD SALARY 0828", 2400m, 8, 28),
            Credit("ACME LTD SALARY 0930", 2450m, 9, 30),
            Credit("ACME LTD SALARY 1030", 2400m, 10, 30)
        ];

        // Act
        DetectedPay pay = PayDetector.Detect(transactions, TimeZoneInfo.Utc, Today).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(pay.AccountId).IsEqualTo(Account.Id);
            await Assert.That(pay.Name).IsEqualTo("ACME LTD SALARY");
            await Assert.That(pay.Key).IsEqualTo("ACMELTDSALARY");
            await Assert.That(pay.Amount).IsEqualTo(2400m);
            await Assert.That(pay.Pattern).IsEqualTo(PayPattern.LastWorkingDay);
            await Assert.That(pay.PayDays).IsEquivalentTo(
                [new DateOnly(2026, 8, 28), new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 30)],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task Detect_TwoPays_IsNothingYet()
    {
        // Arrange
        List<BankTransaction> transactions =
        [
            Credit("ACME LTD SALARY", 2400m, 9, 30),
            Credit("ACME LTD SALARY", 2400m, 10, 30)
        ];

        // Act
        IReadOnlyList<DetectedPay> detected = PayDetector.Detect(transactions, TimeZoneInfo.Utc, Today);

        // Assert
        await Assert.That(detected).IsEmpty();
    }

    [Test]
    public async Task Detect_PaysBeforeTheLastThreeMonths_AreLeftOut()
    {
        // Arrange: June is more than three months back
        List<BankTransaction> transactions =
        [
            Credit("ACME LTD SALARY", 2400m, 6, 30),
            Credit("ACME LTD SALARY", 2400m, 7, 31),
            Credit("ACME LTD SALARY", 2400m, 8, 28)
        ];

        // Act
        IReadOnlyList<DetectedPay> detected = PayDetector.Detect(transactions, TimeZoneInfo.Utc, Today);

        // Assert
        await Assert.That(detected).IsEmpty();
    }

    [Test]
    public async Task Detect_ExpensesFromTheSamePayer_ArePassedOver()
    {
        // Arrange
        List<BankTransaction> transactions =
        [
            Credit("ACME LTD", 2400m, 8, 28),
            Credit("ACME LTD", 2400m, 9, 30),
            Credit("ACME LTD", 45m, 10, 14),
            Credit("ACME LTD", 2400m, 10, 30),
            Credit("ACME LTD", 12m, 11, 2)
        ];

        // Act
        DetectedPay pay = PayDetector.Detect(transactions, TimeZoneInfo.Utc, Today).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(pay.Amount).IsEqualTo(2400m);
            await Assert.That(pay.PayDays).Count().IsEqualTo(3);
        }
    }

    [Test]
    public async Task Detect_TwoIncomes_LargestFirst()
    {
        // Arrange
        List<BankTransaction> transactions =
        [
            Credit("SIDE GIG", 300m, 8, 15),
            Credit("SIDE GIG", 300m, 9, 15),
            Credit("SIDE GIG", 300m, 10, 15),
            Credit("ACME LTD", 2400m, 8, 28),
            Credit("ACME LTD", 2400m, 9, 30),
            Credit("ACME LTD", 2400m, 10, 30)
        ];

        // Act
        IReadOnlyList<DetectedPay> detected = PayDetector.Detect(transactions, TimeZoneInfo.Utc, Today);

        // Assert
        await Assert.That(detected.Select(static x => x.Name)).IsEquivalentTo(["ACME LTD", "SIDE GIG"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Detect_MoneyOutAndPending_AreNotPay()
    {
        // Arrange
        List<BankTransaction> transactions =
        [
            Credit("ACME LTD", -2400m, 8, 28),
            Credit("ACME LTD", 2400m, 9, 30),
            Credit("ACME LTD", 2400m, 10, 30, pending: true)
        ];

        // Act
        IReadOnlyList<DetectedPay> detected = PayDetector.Detect(transactions, TimeZoneInfo.Utc, Today);

        // Assert
        await Assert.That(detected).IsEmpty();
    }

    private static BankTransaction Credit(string description, decimal amount, int month, int day,
        bool pending = false) =>
        BankTransaction.Import(Account, new TransactionDetails(Guid.NewGuid().ToString(), description,
            amount < 0 ? TransactionType.Debit : TransactionType.Credit, "CREDIT", amount, "GBP",
            new DateTime(2026, month, day, 9, 0, 0, DateTimeKind.Utc), pending, [], TransactionMetadata.None,
            false));
}
