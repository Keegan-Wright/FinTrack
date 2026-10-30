using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Tests.Unit.Domain.Bills;

[Category("Domain")]
public class BillMatcherTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly BankAccount Account = new(UserId, Guid.NewGuid(), "acc-1", "TRANSACTION", "Main", "GBP");
    private static readonly BankAccount Other = new(UserId, Guid.NewGuid(), "acc-2", "TRANSACTION", "Joint", "GBP");
    private static readonly DateTime At = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Match_PaymentToThePayeeNearItsDueDate_PaysTheBill()
    {
        // Arrange
        Bill bill = Streaming();
        BankTransaction payment = Payment("NETFLIX.COM 866-579", -10.99m, new DateTime(2026, 2, 12));

        // Act
        int changed = BillMatcher.Match([bill], [payment], TimeZoneInfo.Utc, At);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(changed).IsEqualTo(1);
            await Assert.That(payment.PaysBill(bill.Id)).IsTrue();
            await Assert.That(payment.BillPayment!.Source).IsEqualTo(DecisionSource.Matcher);
        }
    }

    [Test]
    public async Task Match_PayeeInTheMerchantName_PaysTheBill()
    {
        // Arrange
        Bill bill = Streaming();
        BankTransaction payment = Payment("CARD PAYMENT 1234", -10.99m, new DateTime(2026, 2, 10),
            new TransactionMetadata(null, null, "Netflix", null, null));

        // Act
        BillMatcher.Match([bill], [payment], TimeZoneInfo.Utc, At);

        // Assert
        await Assert.That(payment.PaysBill(bill.Id)).IsTrue();
    }

    [Test]
    [Arguments("NETFLIX", -10.99, 2026, 2, 16, "acc-1")]
    [Arguments("NETFLIX", -20, 2026, 2, 10, "acc-1")]
    [Arguments("SPOTIFY", -10.99, 2026, 2, 10, "acc-1")]
    [Arguments("NETFLIX", 10.99, 2026, 2, 10, "acc-1")]
    [Arguments("NETFLIX", -10.99, 2026, 2, 10, "acc-2")]
    public async Task Match_OutsideTheWindowBandPayeeDirectionOrAccount_DoesntMatch(string description,
        double amount, int year, int month, int day, string account)
    {
        // Arrange
        Bill bill = Streaming();
        BankTransaction payment = Payment(description, (decimal)amount, new DateTime(year, month, day),
            account: account == "acc-1" ? Account : Other);

        // Act
        BillMatcher.Match([bill], [payment], TimeZoneInfo.Utc, At);

        // Assert
        await Assert.That(payment.BillPayment).IsNull();
    }

    [Test]
    public async Task Match_PriceRise_StillMatchesWithinTheBand()
    {
        // Arrange
        Bill bill = Streaming();
        BankTransaction payment = Payment("NETFLIX", -12.99m, new DateTime(2026, 2, 10));

        // Act
        BillMatcher.Match([bill], [payment], TimeZoneInfo.Utc, At);

        // Assert
        await Assert.That(payment.PaysBill(bill.Id)).IsTrue();
    }

    [Test]
    public async Task Match_TwoPaymentsForOnePeriod_TheClosestAmountPays()
    {
        // Arrange
        Bill bill = Streaming();
        BankTransaction near = Payment("NETFLIX", -10.99m, new DateTime(2026, 2, 13));
        BankTransaction far = Payment("NETFLIX", -14m, new DateTime(2026, 2, 10));

        // Act
        BillMatcher.Match([bill], [far, near], TimeZoneInfo.Utc, At);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(near.PaysBill(bill.Id)).IsTrue();
            await Assert.That(far.BillPayment).IsNull();
        }
    }

    [Test]
    public async Task Match_PeriodTheUserPaid_IsLeftToThem()
    {
        // Arrange
        Bill bill = Streaming();
        BankTransaction usersPayment = Payment("NETFLIX GIFT", -5m, new DateTime(2026, 2, 9));
        usersPayment.PayBill(bill, At);
        BankTransaction another = Payment("NETFLIX", -10.99m, new DateTime(2026, 2, 10));

        // Act
        BillMatcher.Match([bill], [usersPayment, another], TimeZoneInfo.Utc, At);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(another.BillPayment).IsNull();
            await Assert.That(usersPayment.BillPayment!.Source).IsEqualTo(DecisionSource.User);
        }
    }

    [Test]
    public async Task Match_UserSaidItPaysNoBill_StaysUnpaid()
    {
        // Arrange
        Bill bill = Streaming();
        BankTransaction payment = Payment("NETFLIX", -10.99m, new DateTime(2026, 2, 10));
        payment.PayNoBill(At);

        // Act
        int changed = BillMatcher.Match([bill], [payment], TimeZoneInfo.Utc, At);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(changed).IsEqualTo(0);
            await Assert.That(payment.BillPayment!.BillId).IsNull();
        }
    }

    [Test]
    public async Task Match_MatchNoLongerFits_IsTakenBack()
    {
        // Arrange
        Bill bill = Streaming();
        BankTransaction payment = Payment("NETFLIX", -10.99m, new DateTime(2026, 2, 10));
        BillMatcher.Match([bill], [payment], TimeZoneInfo.Utc, At);
        bill.ChangePayee("Disney");

        // Act
        int changed = BillMatcher.Match([bill], [payment], TimeZoneInfo.Utc, At);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(changed).IsEqualTo(1);
            await Assert.That(payment.BillPayment).IsNull();
        }
    }

    [Test]
    public async Task Match_Again_ChangesNothing()
    {
        // Arrange
        Bill bill = Streaming();
        BankTransaction payment = Payment("NETFLIX", -10.99m, new DateTime(2026, 2, 10));
        BillMatcher.Match([bill], [payment], TimeZoneInfo.Utc, At);

        // Act
        int changed = BillMatcher.Match([bill], [payment], TimeZoneInfo.Utc, At.AddHours(1));

        // Assert
        await Assert.That(changed).IsEqualTo(0);
    }

    [Test]
    public async Task Match_AfterTheBillEnded_DoesntMatch()
    {
        // Arrange
        Bill bill = Streaming();
        bill.End(new DateOnly(2026, 1, 31));
        BankTransaction payment = Payment("NETFLIX", -10.99m, new DateTime(2026, 2, 10));

        // Act
        BillMatcher.Match([bill], [payment], TimeZoneInfo.Utc, At);

        // Assert
        await Assert.That(payment.BillPayment).IsNull();
    }

    [Test]
    public async Task Match_OnePaymentTwoBills_TheCloserInAmountPays()
    {
        // Arrange
        Bill basic = Streaming(6.99m);
        Bill premium = Streaming(17.99m);
        BankTransaction payment = Payment("NETFLIX", -17.99m, new DateTime(2026, 2, 10));

        // Act
        BillMatcher.Match([basic, premium], [payment], TimeZoneInfo.Utc, At);

        // Assert
        await Assert.That(payment.PaysBill(premium.Id)).IsTrue();
    }

    [Test]
    [Arguments("Netflix", "NETFLIX.COM LONDON", true)]
    [Arguments("Netflix International", "NETFLIX", true)]
    [Arguments("British Gas", "SKY", false)]
    [Arguments("Thames Water", "THAMESWATER DD", true)]
    public async Task IsPaidTo_IgnoresCaseSpacesAndPunctuation(string payee, string description, bool paid) =>
        await Assert.That(BillMatcher.IsPaidTo(
                new Bill(UserId, Account.Id, payee, payee, BillFrequency.Monthly, 10m, new DateOnly(2026, 1, 10),
                    BillSource.Manual),
                Payment(description, -10m, new DateTime(2026, 2, 10))))
            .IsEqualTo(paid);

    private static Bill Streaming(decimal amount = 10.99m) =>
        new(UserId, Account.Id, "Netflix", "Netflix", BillFrequency.Monthly, amount, new DateOnly(2026, 1, 10),
            BillSource.Manual);

    private static BankTransaction Payment(string description, decimal amount, DateTime occurredAt,
        TransactionMetadata? metadata = null, BankAccount? account = null) =>
        BankTransaction.Import(account ?? Account, new TransactionDetails(Guid.NewGuid().ToString(), description,
            amount < 0 ? TransactionType.Debit : TransactionType.Credit, "PURCHASE", amount, "GBP",
            DateTime.SpecifyKind(occurredAt, DateTimeKind.Utc), false, [], metadata ?? TransactionMetadata.None,
            false));
}
