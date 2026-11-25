using System.Text.Json;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Ai;

public class BillToolsTests : AiToolTestBase
{
    private readonly IDashboardService _dashboardService = Substitute.For<IDashboardService>();

    public BillToolsTests()
    {
        Services.AddSingleton(_dashboardService);
        Services.AddSingleton(Substitute.For<IBillsService>());
        _dashboardService.GetUpcomingPaymentsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => new[]
            {
                Payment("Late bill", new DateOnly(2026, 6, 5), BillState.Late),
                Payment("Soon bill", new DateOnly(2026, 6, 25), BillState.Upcoming),
                Payment("Later bill", new DateOnly(2026, 7, 15), BillState.Upcoming)
            }.ToAsyncEnumerable());
    }

    [Test]
    public async Task GetUpcomingPayments_WithinDays_KeepsThoseDueByThenAndLateOnes()
    {
        // Act, today is 2026-06-15
        JsonElement upcoming = await CallAsync("get_upcoming_payments", new { days = 10 });

        // Assert
        await Assert.That(Names(upcoming)).IsEquivalentTo(["Late bill", "Soon bill"]);
    }

    [Test]
    public async Task GetUpcomingPayments_NoDays_GivesTheNextWheneverDue()
    {
        // Act
        JsonElement upcoming = await CallAsync("get_upcoming_payments");

        // Assert
        await Assert.That(Names(upcoming)).IsEquivalentTo(["Late bill", "Soon bill", "Later bill"]);
    }

    [Test]
    public async Task GetUpcomingPayments_SaysWhichCurrencyTheAmountsAreIn()
    {
        // Arrange
        using CultureScope culture = new("en-GB");

        // Act
        JsonElement upcoming = await CallAsync("get_upcoming_payments");

        // Assert
        await Assert.That(upcoming.GetProperty("currency").GetString()).IsEqualTo("GBP");
    }

    private static List<string> Names(JsonElement upcoming) =>
        upcoming.GetProperty("payments").EnumerateArray().Select(x => x.GetProperty("name").GetString()!).ToList();

    private static UpcomingPayment Payment(string name, DateOnly due, BillState state) =>
        new() { PaymentName = name, PaymentType = "Subscription", Amount = 9.99m, Due = due, State = state };
}
