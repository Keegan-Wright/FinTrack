using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Infrastructure.Dashboard;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Dashboard;

public class DashboardServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    private static readonly DateTime Now = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task GetSpentInTimePeriod_SplitsInOutTotals()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("salary", "main", Transaction(100m, Now, "CREDIT")))
            .Do(s => s.TransactionScenario.WithTransaction("shopping", "main", Transaction(-25m, Now, "PURCHASE")))
            .Do(s => s.TransactionScenario.WithTransaction("bill", "main", Transaction(-10m, Now, "BILL_PAYMENT")));

        DashboardService subject = BuildSubject();

        // Act
        SpentInTimePeriod result = await subject.GetSpentInTimePeriod(Now.AddDays(-1), Now.AddDays(1), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.TotalIn).IsEqualTo(100m);
            await Assert.That(result.TotalOut).IsEqualTo(-35m);
        }
    }

    [Test]
    public async Task GetSpentInTimePeriod_IncludesTheStartAndLeavesOutTheEnd()
    {
        // Arrange
        DateTime from = new(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = new(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc);

        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("at-start", "main", Transaction(-1m, from)))
            .Do(s => s.TransactionScenario.WithTransaction("before-end", "main", Transaction(-2m, to.AddSeconds(-1))))
            .Do(s => s.TransactionScenario.WithTransaction("before-start", "main", Transaction(-4m, from.AddSeconds(-1))))
            .Do(s => s.TransactionScenario.WithTransaction("at-end", "main", Transaction(-8m, to)));

        DashboardService subject = BuildSubject();

        // Act
        SpentInTimePeriod result = await subject.GetSpentInTimePeriod(from, to, CancellationToken);

        // Assert
        await Assert.That(result.TotalOut).IsEqualTo(-3m);
    }

    [Test]
    public async Task GetSpentInTimePeriod_OnlyIncludesCurrentUsersTransactions()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("other-bank", OtherUsername))
            .Do(s => s.BankAccountScenario.WithAccount("other", "other-bank"))
            .Do(s => s.TransactionScenario.WithTransaction("theirs", "other", Transaction(-50m, Now)));

        DashboardService subject = BuildSubject();

        // Act
        SpentInTimePeriod result = await subject.GetSpentInTimePeriod(Now.AddDays(-1), Now.AddDays(1), CancellationToken);

        // Assert
        await Assert.That(result.TotalOut).IsEqualTo(0m);
    }

    [Test]
    public async Task GetSpentInTimePeriod_WhenUserHasNoTransactions_ReturnsZeroTotals()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        DashboardService subject = BuildSubject();

        // Act
        SpentInTimePeriod result = await subject.GetSpentInTimePeriod(Now.AddDays(-1), Now.AddDays(1), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.TotalIn).IsEqualTo(0m);
            await Assert.That(result.TotalOut).IsEqualTo(0m);
        }
    }

    [Test]
    public async Task GetUpcomingPaymentsAsync_IncludesFutureStandingOrdersOnly()
    {
        // Arrange
        StandingOrderDetails upcoming = new StandingOrderDetailsFaker().Generate() with
        {
            Payee = "Landlord", NextPaymentDate = Now.AddDays(3), NextPaymentAmount = 750m
        };
        StandingOrderDetails lapsed = new StandingOrderDetailsFaker().Generate() with
        {
            Payee = "Old Gym", NextPaymentDate = Now.AddDays(-3)
        };

        await SeedAccountAsync()
            .Do(s => s.BankAccountScenario.WithStandingOrders("main", upcoming, lapsed));

        DashboardService subject = BuildSubject();

        // Act
        List<UpcomingPayment> payments = await subject.GetUpcomingPaymentsAsync(10, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        UpcomingPayment payment = payments.Single();

        using (Assert.Multiple())
        {
            await Assert.That(payment.PaymentName).IsEqualTo("Landlord");
            await Assert.That(payment.PaymentType).IsEqualTo("Standing Order");
            await Assert.That(payment.Amount).IsEqualTo(750m);
            await Assert.That(payment.PaymentDate).IsEqualTo(upcoming.NextPaymentDate);
        }
    }

    [Test]
    public async Task GetUpcomingPaymentsAsync_MapsDirectDebit()
    {
        // Arrange
        DirectDebitDetails directDebit = new DirectDebitDetailsFaker().Generate() with
        {
            Name = "Energy", PreviousPaymentDate = Now.AddDays(-1), PreviousPaymentAmount = 80m
        };

        await SeedAccountAsync()
            .Do(s => s.BankAccountScenario.WithDirectDebits("main", directDebit));

        DashboardService subject = BuildSubject();

        // Act
        List<UpcomingPayment> payments = await subject.GetUpcomingPaymentsAsync(10, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        UpcomingPayment payment = payments.Single();

        using (Assert.Multiple())
        {
            await Assert.That(payment.PaymentName).IsEqualTo("Energy");
            await Assert.That(payment.PaymentType).IsEqualTo("Direct Debit");
            await Assert.That(payment.Amount).IsEqualTo(80m);
        }
    }

    [Test]
    // Paid earlier this month, next is a month on
    [Arguments("2026-01-05T09:00:00", "2026-01-10T12:00:00", "2026-02-05T09:00:00")]
    // Paid last month and still to come this month
    [Arguments("2025-12-15T09:00:00", "2026-01-10T12:00:00", "2026-01-15T09:00:00")]
    // Paid last month and already due this month
    [Arguments("2025-12-05T09:00:00", "2026-01-10T12:00:00", "2026-02-05T09:00:00")]
    // Last known payment months ago
    [Arguments("2024-06-20T09:00:00", "2026-01-10T12:00:00", "2026-01-20T09:00:00")]
    // Short months don't drift the payment day
    [Arguments("2026-01-31T09:00:00", "2026-03-29T12:00:00", "2026-03-31T09:00:00")]
    [Arguments("2026-01-31T09:00:00", "2026-02-10T12:00:00", "2026-02-28T09:00:00")]
    // Due later today
    [Arguments("2025-12-10T13:00:00", "2026-01-10T12:00:00", "2026-01-10T13:00:00")]
    public async Task GetUpcomingPaymentsAsync_ProjectsDirectDebitToNextMonthlyPayment(string previousPayment, string now,
        string expectedNextPayment)
    {
        // Arrange
        DirectDebitDetails directDebit = new DirectDebitDetailsFaker().Generate() with
        {
            PreviousPaymentDate = Utc(previousPayment), PreviousPaymentAmount = 80m
        };

        await SeedAccountAsync()
            .Do(s => s.BankAccountScenario.WithDirectDebits("main", directDebit));

        DashboardService subject = BuildSubject(Utc(now));

        // Act
        List<UpcomingPayment> payments = await subject.GetUpcomingPaymentsAsync(10, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(payments.Single().PaymentDate).IsEqualTo(Utc(expectedNextPayment));
    }

    [Test]
    public async Task GetUpcomingPaymentsAsync_IgnoresDirectDebitsWithNoPreviousPayment()
    {
        // Arrange
        DirectDebitDetails directDebit = new DirectDebitDetailsFaker().Generate() with
        {
            PreviousPaymentDate = Now.AddDays(-5), PreviousPaymentAmount = 0m
        };

        await SeedAccountAsync()
            .Do(s => s.BankAccountScenario.WithDirectDebits("main", directDebit));

        DashboardService subject = BuildSubject();

        // Act
        List<UpcomingPayment> payments = await subject.GetUpcomingPaymentsAsync(10, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(payments.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetUpcomingPaymentsAsync_OrdersByDateAndTakesRequestedNumber()
    {
        // Arrange
        StandingOrderDetails[] standingOrders = Enumerable.Range(1, 4)
            .Select(i => new StandingOrderDetailsFaker().Generate() with
            {
                Payee = $"Payee {i}", NextPaymentDate = Now.AddDays(10 - i)
            })
            .ToArray();
        DirectDebitDetails directDebit = new DirectDebitDetailsFaker().Generate() with
        {
            Name = "Direct Debit", PreviousPaymentDate = Now.AddDays(1).AddMonths(-1), PreviousPaymentAmount = 10m
        };

        await SeedAccountAsync()
            .Do(s => s.BankAccountScenario.WithStandingOrders("main", standingOrders))
            .Do(s => s.BankAccountScenario.WithDirectDebits("main", directDebit));

        DashboardService subject = BuildSubject();

        // Act
        List<UpcomingPayment> payments = await subject.GetUpcomingPaymentsAsync(3, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(string.Join(",", payments.Select(x => x.PaymentName)))
            .IsEqualTo("Direct Debit,Payee 4,Payee 3");
    }

    [Test]
    public async Task GetUpcomingPaymentsAsync_OnlyIncludesCurrentUsersPayments()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("other-bank", OtherUsername))
            .Do(s => s.BankAccountScenario.WithAccount("other", "other-bank"))
            .Do(s => s.BankAccountScenario.WithStandingOrders("other", new StandingOrderDetailsFaker().Generate() with
            {
                NextPaymentDate = Now.AddDays(2)
            }))
            .Do(s => s.BankAccountScenario.WithDirectDebits("other", new DirectDebitDetailsFaker().Generate() with
            {
                PreviousPaymentDate = Now.AddDays(-2), PreviousPaymentAmount = 10m
            }));

        DashboardService subject = BuildSubject();

        // Act
        List<UpcomingPayment> payments = await subject.GetUpcomingPaymentsAsync(10, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(payments.Count).IsEqualTo(0);
    }

    private Task<TestingScenario> SeedAccountAsync() =>
        Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"));

    private static Domain.OpenBanking.Transactions.TransactionDetails Transaction(decimal amount, DateTime occurredAt,
        string category = "PURCHASE") =>
        new TransactionDetailsFaker().Generate() with { Amount = amount, OccurredAt = occurredAt, Category = category };

    private static DateTime Utc(string value) =>
        DateTime.SpecifyKind(DateTime.Parse(value, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);

    private DashboardService BuildSubject(DateTime? now = null) => new(CurrentUserFor(Username), DbContextFactory,
        NullLogger<DashboardService>.Instance, new FixedTimeProvider(now ?? Now));
}
