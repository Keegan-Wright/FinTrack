using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.OpenBanking.Accounts;

namespace FinanceTracker.Tests.Unit.Domain.Bills;

[Category("Domain")]
public class BillSeederTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 10, 30);

    [Test]
    public async Task Seed_NewStandingOrder_AddsABillOnItsSchedule()
    {
        // Arrange
        BankAccount account = Account();
        account.SyncStandingOrders([StandingOrder("IntrvlMnthDay:01:15", next: new DateTime(2026, 11, 15),
            amount: -650m, first: new DateTime(2025, 3, 15))]);

        // Act
        BillSeedSummary summary = BillSeeder.Seed([account], [], Today);

        // Assert
        Bill bill = summary.Added.Single();
        using (Assert.Multiple())
        {
            await Assert.That(bill.Source).IsEqualTo(BillSource.StandingOrder);
            await Assert.That(bill.SourceId).IsEqualTo(account.StandingOrders.Single().Id);
            await Assert.That(bill.AccountId).IsEqualTo(account.Id);
            await Assert.That(bill.Name).IsEqualTo("Landlord");
            await Assert.That(bill.Payee).IsEqualTo("Landlord");
            await Assert.That(bill.Frequency).IsEqualTo(BillFrequency.Monthly);
            await Assert.That(bill.ExpectedAmount).IsEqualTo(650m);
            await Assert.That(bill.StartsOn).IsEqualTo(new DateOnly(2025, 3, 15));
            await Assert.That(bill.NextDue(Today)).IsEqualTo(new DateOnly(2026, 11, 15));
            await Assert.That(bill.HasEnded).IsFalse();
        }
    }

    [Test]
    public async Task Seed_UnknownFrequency_AssumesMonthly()
    {
        // Arrange
        BankAccount account = Account();
        account.SyncStandingOrders([StandingOrder("EvryWorkgDay")]);

        // Act
        BillSeedSummary summary = BillSeeder.Seed([account], [], Today);

        // Assert
        await Assert.That(summary.Added.Single().Frequency).IsEqualTo(BillFrequency.Monthly);
    }

    [Test]
    public async Task Seed_StandingOrderWithAFinalPayment_EndsThen()
    {
        // Arrange
        BankAccount account = Account();
        account.SyncStandingOrders([StandingOrder(final: new DateTime(2027, 3, 15))]);

        // Act
        BillSeedSummary summary = BillSeeder.Seed([account], [], Today);

        // Assert
        await Assert.That(summary.Added.Single().EndedOn).IsEqualTo(new DateOnly(2027, 3, 15));
    }

    [Test]
    [Arguments("Inactive", -650, 2026, 11, 15)]
    [Arguments("Active", 0, 2026, 11, 15)]
    [Arguments("Active", -650, 1, 1, 1)]
    public async Task Seed_StandingOrderStoppedOrMissingDetails_AddsNothing(string status, double amount, int year,
        int month, int day)
    {
        // Arrange
        BankAccount account = Account();
        account.SyncStandingOrders([StandingOrder(next: new DateTime(year, month, day), amount: (decimal)amount,
            status: status)]);

        // Act
        BillSeedSummary summary = BillSeeder.Seed([account], [], Today);

        // Assert
        await Assert.That(summary.Added).IsEmpty();
    }

    [Test]
    public async Task Seed_KnownStandingOrder_FollowsItsScheduleAndPriceWithoutAddingAnother()
    {
        // Arrange
        BankAccount account = Account();
        account.SyncStandingOrders([StandingOrder(next: new DateTime(2026, 11, 15), amount: -650m)]);
        Bill bill = BillSeeder.Seed([account], [], Today).Added.Single();
        bill.Rename("Rent");
        account.SyncStandingOrders([StandingOrder(next: new DateTime(2026, 12, 20), amount: -675m)]);

        // Act
        BillSeedSummary summary = BillSeeder.Seed([account], [bill], Today);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(summary.Added).IsEmpty();
            await Assert.That(bill.Name).IsEqualTo("Rent");
            await Assert.That(bill.NextDue(Today)).IsEqualTo(new DateOnly(2026, 11, 20));
            await Assert.That(bill.ExpectedOn(new DateOnly(2026, 11, 20))).IsEqualTo(650m);
            await Assert.That(bill.ExpectedOn(new DateOnly(2026, 12, 20))).IsEqualTo(675m);
        }
    }

    [Test]
    public async Task Seed_StandingOrderGone_EndsItsBillToday()
    {
        // Arrange
        BankAccount account = Account();
        account.SyncStandingOrders([StandingOrder()]);
        Bill bill = BillSeeder.Seed([account], [], Today).Added.Single();
        account.SyncStandingOrders([]);

        // Act
        BillSeedSummary summary = BillSeeder.Seed([account], [bill], Today);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(summary.Ended).IsEqualTo(1);
            await Assert.That(bill.EndedOn).IsEqualTo(Today);
        }
    }

    [Test]
    public async Task Seed_StandingOrderStopsThenRestarts_EndsThenReopens()
    {
        // Arrange
        BankAccount account = Account();
        account.SyncStandingOrders([StandingOrder()]);
        Bill bill = BillSeeder.Seed([account], [], Today).Added.Single();
        account.SyncStandingOrders([StandingOrder(status: "Inactive")]);
        BillSeeder.Seed([account], [bill], Today);
        bool endedWhileInactive = bill.HasEnded;
        account.SyncStandingOrders([StandingOrder()]);

        // Act
        BillSeeder.Seed([account], [bill], Today);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(endedWhileInactive).IsTrue();
            await Assert.That(bill.HasEnded).IsFalse();
        }
    }

    [Test]
    public async Task Seed_StandingOrderStillRunningButTheUserEndedTheBill_StaysEnded()
    {
        // Arrange
        BankAccount account = Account();
        account.SyncStandingOrders([StandingOrder()]);
        Bill bill = BillSeeder.Seed([account], [], Today).Added.Single();
        bill.EndByUser(Today);

        // Act
        BillSeeder.Seed([account], [bill], Today);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(bill.HasEnded).IsTrue();
            await Assert.That(bill.EndedByUser).IsTrue();
        }
    }

    [Test]
    public async Task Seed_NewDirectDebit_AddsAMonthlyBillFromItsPreviousPayment()
    {
        // Arrange
        BankAccount account = Account();
        account.SyncDirectDebits([DirectDebit(previous: new DateTime(2026, 10, 3), amount: -42.10m)]);

        // Act
        BillSeedSummary summary = BillSeeder.Seed([account], [], Today);

        // Assert
        Bill bill = summary.Added.Single();
        using (Assert.Multiple())
        {
            await Assert.That(bill.Source).IsEqualTo(BillSource.DirectDebit);
            await Assert.That(bill.Name).IsEqualTo("Energy Co");
            await Assert.That(bill.Frequency).IsEqualTo(BillFrequency.Monthly);
            await Assert.That(bill.ExpectedAmount).IsEqualTo(42.10m);
            await Assert.That(bill.NextDue(Today)).IsEqualTo(new DateOnly(2026, 11, 3));
        }
    }

    [Test]
    public async Task Seed_DirectDebitNeverPaid_AddsNothing()
    {
        // Arrange
        BankAccount account = Account();
        account.SyncDirectDebits([DirectDebit(amount: 0)]);

        // Act & Assert
        await Assert.That(BillSeeder.Seed([account], [], Today).Added).IsEmpty();
    }

    [Test]
    public async Task Seed_KnownDirectDebit_KeepsTheUsersSchedule()
    {
        // Arrange
        BankAccount account = Account();
        account.SyncDirectDebits([DirectDebit()]);
        Bill bill = BillSeeder.Seed([account], [], Today).Added.Single();
        bill.Reschedule(BillFrequency.Quarterly, new DateOnly(2026, 12, 1));

        // Act
        BillSeeder.Seed([account], [bill], Today);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(bill.Frequency).IsEqualTo(BillFrequency.Quarterly);
            await Assert.That(bill.Anchor).IsEqualTo(new DateOnly(2026, 12, 1));
        }
    }

    [Test]
    public async Task Seed_ManualBill_IsLeftAlone()
    {
        // Arrange
        BankAccount account = Account();
        Bill manual = new(UserId, account.Id, "Gym", "Gymco", BillFrequency.Monthly, 30m, new DateOnly(2026, 1, 1),
            BillSource.Manual);

        // Act
        BillSeedSummary summary = BillSeeder.Seed([account], [manual], Today);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(summary.Ended).IsEqualTo(0);
            await Assert.That(manual.HasEnded).IsFalse();
        }
    }

    private static BankAccount Account() => new(UserId, Guid.NewGuid(), "acc-1", "TRANSACTION", "Main", "GBP");

    private static StandingOrderDetails StandingOrder(string frequency = "Monthly", DateTime? next = null,
        decimal amount = -650m, DateTime? first = null, DateTime? final = null, string status = "Active") =>
        new("RENT", "Landlord", frequency, status, "GBP", next ?? new DateTime(2026, 11, 15), amount,
            first ?? default, amount, final ?? default, amount, new DateTime(2026, 10, 30));

    private static DirectDebitDetails DirectDebit(DateTime? previous = null, decimal amount = -42.10m,
        string status = "Active") =>
        new("dd-1", "Energy Co", status, previous ?? new DateTime(2026, 10, 3), amount, "GBP",
            new DateTime(2026, 10, 30));
}
