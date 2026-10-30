using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Tests.Unit.Domain.Bills;

[Category("Domain")]
public class BillCalculatorTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly BankAccount Account = new(UserId, Guid.NewGuid(), "acc-1", "TRANSACTION", "Main", "GBP");
    private static readonly DateOnly January10 = new(2026, 1, 10);
    private static readonly DateOnly February10 = new(2026, 2, 10);
    private static readonly DateOnly March10 = new(2026, 3, 10);

    [Test]
    public async Task Periods_PaidWithinTolerance_IsPaid()
    {
        // Arrange
        Bill bill = Monthly(10m);
        BankTransaction payment = Paying(bill, -10.20m, new DateTime(2026, 2, 11, 9, 0, 0));

        // Act
        BillPeriod period = Single(bill, [payment], February10, new DateOnly(2026, 2, 20));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(period.State).IsEqualTo(BillState.Paid);
            await Assert.That(period.Paid).IsEqualTo(10.20m);
            await Assert.That(period.PaidOn).IsEqualTo(new DateOnly(2026, 2, 11));
            await Assert.That(period.PaymentIds).IsEquivalentTo([payment.Id]);
        }
    }

    [Test]
    public async Task Periods_PaidOutsideTolerance_IsChanged()
    {
        // Arrange
        Bill bill = Monthly(10m);
        BankTransaction payment = Paying(bill, -12.99m, new DateTime(2026, 2, 10, 9, 0, 0));

        // Act
        BillPeriod period = Single(bill, [payment], February10, new DateOnly(2026, 2, 20));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(period.State).IsEqualTo(BillState.Changed);
            await Assert.That(period.Expected).IsEqualTo(10m);
        }
    }

    [Test]
    public async Task Periods_PriceChangedFromTheDayAfter_ThatPeriodStaysChangedAndTheNextExpectsIt()
    {
        // Arrange
        Bill bill = Monthly(10m);
        BankTransaction february = Paying(bill, -12.99m, new DateTime(2026, 2, 10, 9, 0, 0));
        BankTransaction march = Paying(bill, -12.99m, new DateTime(2026, 3, 10, 9, 0, 0));
        bill.ChangeAmount(12.99m, February10.AddDays(1));

        // Act
        IReadOnlyList<BillPeriod> periods = BillCalculator.Periods(bill, [february, march], February10, March10,
            TimeZoneInfo.Utc, new DateOnly(2026, 3, 20));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(periods[0].State).IsEqualTo(BillState.Changed);
            await Assert.That(periods[1].State).IsEqualTo(BillState.Paid);
            await Assert.That(periods[1].Expected).IsEqualTo(12.99m);
        }
    }

    [Test]
    [Arguments(2026, 2, 13, BillState.Upcoming)]
    [Arguments(2026, 2, 14, BillState.Late)]
    [Arguments(2026, 1, 20, BillState.Upcoming)]
    public async Task Periods_Unpaid_IsLateOnlyOnceGraceHasPassed(int year, int month, int day, BillState state) =>
        await Assert.That(Single(Monthly(10m), [], February10, new DateOnly(year, month, day)).State)
            .IsEqualTo(state);

    [Test]
    public async Task Periods_PaymentForAnotherBill_DoesntCount()
    {
        // Arrange
        Bill bill = Monthly(10m);
        Bill other = Monthly(10m);
        BankTransaction payment = Paying(other, -10m, new DateTime(2026, 2, 10, 9, 0, 0));

        // Act
        BillPeriod period = Single(bill, [payment], February10, new DateOnly(2026, 2, 20));

        // Assert
        await Assert.That(period.State).IsEqualTo(BillState.Late);
    }

    [Test]
    public async Task Periods_EarlyPayment_CountsForTheNearestDueDate()
    {
        // Arrange
        Bill bill = Monthly(10m);
        BankTransaction payment = Paying(bill, -10m, new DateTime(2026, 3, 7, 9, 0, 0));

        // Act
        IReadOnlyList<BillPeriod> periods = BillCalculator.Periods(bill, [payment], February10, March10,
            TimeZoneInfo.Utc, new DateOnly(2026, 3, 8));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(periods[0].State).IsEqualTo(BillState.Late);
            await Assert.That(periods[1].State).IsEqualTo(BillState.Paid);
        }
    }

    [Test]
    public async Task Periods_PaymentLateEveningUtcInSummer_FallsOnTheNextDayWhereTheUserIs()
    {
        // Arrange
        TimeZoneInfo london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        Bill bill = new(UserId, Account.Id, "Gym", "Gymco", BillFrequency.Monthly, 30m, new DateOnly(2026, 6, 1),
            BillSource.Manual);
        BankTransaction payment = Paying(bill, -30m, new DateTime(2026, 6, 30, 23, 30, 0));

        // Act
        BillPeriod period = BillCalculator.Periods(bill, [payment], new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 1), london, new DateOnly(2026, 7, 2)).Single();

        // Assert
        await Assert.That(period.PaidOn).IsEqualTo(new DateOnly(2026, 7, 1));
    }

    [Test]
    public async Task Periods_RefundedInFull_PaysNothing()
    {
        // Arrange
        Bill bill = Monthly(10m);
        BankTransaction payment = Paying(bill, -10m, new DateTime(2026, 2, 10, 9, 0, 0));
        BankTransaction refund = Paying(bill, 10m, new DateTime(2026, 2, 11, 9, 0, 0));

        // Act
        BillPeriod period = Single(bill, [payment, refund], February10, new DateOnly(2026, 2, 20));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(period.Paid).IsEqualTo(0m);
            await Assert.That(period.State).IsEqualTo(BillState.Changed);
        }
    }

    [Test]
    [Arguments(BillFrequency.Weekly, 43.33)]
    [Arguments(BillFrequency.Fortnightly, 21.67)]
    [Arguments(BillFrequency.FourWeekly, 10.83)]
    [Arguments(BillFrequency.Monthly, 10)]
    [Arguments(BillFrequency.Quarterly, 3.33)]
    [Arguments(BillFrequency.Yearly, 0.83)]
    public async Task MonthlyCost_SpreadsTheAmountOverAMonth(BillFrequency frequency, double cost)
    {
        // Arrange
        Bill bill = new(UserId, Account.Id, "Bill", "Payee", frequency, 10m, January10, BillSource.Manual);

        // Act & Assert
        await Assert.That(BillCalculator.MonthlyCost(bill, February10)).IsEqualTo((decimal)cost);
    }

    [Test]
    public async Task MonthlyCost_EndedBefore_IsNothing()
    {
        // Arrange
        Bill bill = Monthly(10m);
        bill.End(February10);

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(BillCalculator.MonthlyCost(bill, February10)).IsEqualTo(10m);
            await Assert.That(BillCalculator.MonthlyCost(bill, March10)).IsEqualTo(0m);
        }
    }

    private static Bill Monthly(decimal amount) =>
        new(UserId, Account.Id, "Streaming", "Streamco", BillFrequency.Monthly, amount, January10, BillSource.Manual);

    private static BillPeriod Single(Bill bill, IEnumerable<BankTransaction> transactions, DateOnly due,
        DateOnly today) =>
        BillCalculator.Periods(bill, transactions, due, due, TimeZoneInfo.Utc, today).Single();

    private static BankTransaction Paying(Bill bill, decimal amount, DateTime occurredAt)
    {
        BankTransaction transaction = BankTransaction.Import(Account, new TransactionDetails(
            Guid.NewGuid().ToString(), "STREAMCO", amount < 0 ? TransactionType.Debit : TransactionType.Credit,
            "PURCHASE", amount, "GBP", DateTime.SpecifyKind(occurredAt, DateTimeKind.Utc), false, [],
            TransactionMetadata.None, false));

        transaction.DecideBillPayment(BillPayment.Matched(bill.Id, occurredAt));
        return transaction;
    }
}
