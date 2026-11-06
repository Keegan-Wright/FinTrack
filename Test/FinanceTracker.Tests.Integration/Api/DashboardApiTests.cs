using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using BillState = FinanceTracker.Contracts.Bills.BillState;

namespace FinanceTracker.Tests.Integration.Api;

/// <summary>
/// The dashboard's figures through the API, on 15 Oct 2026 for the user, see <c>TestClock</c>.
/// </summary>
public class DashboardApiTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Today = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Spent_InARange_CountsOnlyItsMoneyInAndOut()
    {
        // Arrange, two in October and one in September
        SignedInUser user = await RegisterUserAsync(at: Today);
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("pay", "main", Details("ACME LTD", 1200m, 10, 1)))
            .Do(s => s.TransactionScenario.WithTransaction("shop", "main", Details("CORNER SHOP", -45.5m, 10, 12)))
            .Do(s => s.TransactionScenario.WithTransaction("before", "main", Details("CORNER SHOP", -9m, 9, 28)));
        IDashboardService service = ApiFor(user).Get<IDashboardService>();

        // Act
        SpentInTimePeriod october = await service.GetSpentInTimePeriod(new DateTime(2026, 10, 1, 0, 0, 0,
            DateTimeKind.Utc), new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(october.TotalIn).IsEqualTo(1200m);
            await Assert.That(october.TotalOut).IsEqualTo(-45.5m);
        }
    }

    [Test]
    public async Task UpcomingPayments_ABillDueSoon_ListsItWhenItsDue()
    {
        // Arrange, due on the 20th each month, today's the 15th
        SignedInUser user = await RegisterUserAsync(at: Today);
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.BillScenario.WithBill("gym", user.Username, "main", static (userId, accountId) =>
                new Bill(userId, accountId, "Gym", "GYM CO", BillFrequency.Monthly, 30m, new DateOnly(2026, 9, 20),
                    BillSource.Manual)));
        IDashboardService service = ApiFor(user).Get<IDashboardService>();

        // Act
        List<UpcomingPayment> upcoming = await service.GetUpcomingPaymentsAsync(3, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        UpcomingPayment next = upcoming.First();
        using (Assert.Multiple())
        {
            await Assert.That(next.PaymentName).IsEqualTo("Gym");
            await Assert.That(next.Due).IsEqualTo(new DateOnly(2026, 10, 20));
            await Assert.That(next.Amount).IsEqualTo(30m);
            await Assert.That(next.State).IsEqualTo(BillState.Upcoming);
        }
    }

    private static TransactionDetails Details(string description, decimal amount, int month, int day)
    {
        TransactionType type = TransactionType.Debit;
        if (amount > 0)
        {
            type = TransactionType.Credit;
        }

        return new TransactionDetails($"tx-{Guid.NewGuid():N}", description, type, "PURCHASE", amount, "GBP",
            new DateTime(2026, month, day, 12, 0, 0, DateTimeKind.Utc), false, [], TransactionMetadata.None, false);
    }
}
