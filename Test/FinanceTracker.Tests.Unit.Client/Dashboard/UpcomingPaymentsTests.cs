using Bunit;
using FinanceTracker.Client.Components.Dashboard;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Dashboard;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Unit.Client.Dashboard;

public class UpcomingPaymentsTests : ComponentTestBase
{
    public UpcomingPaymentsTests() =>
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTime(2026, 10, 30, 12, 0, 0,
            DateTimeKind.Utc)));

    [Test]
    public async Task Render_PaidTickedOffLateFlaggedAndUpcomingCountedDown()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        IRenderedComponent<UpcomingPayments> cut = Render<UpcomingPayments>(p => p.Add(x => x.Payments,
        [
            Payment("Gym", BillState.Late, new DateOnly(2026, 10, 24)),
            Payment("Streaming", BillState.Paid, new DateOnly(2026, 10, 28)),
            Payment("Rent", BillState.Upcoming, new DateOnly(2026, 11, 2))
        ]));

        // Assert
        List<string> rows = cut.FindAll(".ft-list__row").Select(static x => x.TextContent).ToList();
        using (Assert.Multiple())
        {
            await Assert.That(rows[0]).Contains("late");
            await Assert.That(rows[1]).Contains("paid");
            await Assert.That(rows[2]).Contains("in 3 days");
            await Assert.That(cut.FindAll("[aria-label='Paid']")).Count().IsEqualTo(1);
        }
    }

    [Test]
    public async Task Render_NoBills_SaysWhereTheyComeFrom() =>
        await Assert.That(Render<UpcomingPayments>(p => p.Add(x => x.Payments, [])).Markup)
            .Contains("Your standing orders and direct debits show here after a sync");

    private static UpcomingPayment Payment(string name, BillState state, DateOnly due) => new()
    {
        BillId = Guid.NewGuid(), PaymentName = name, Amount = 10m, Due = due, State = state,
        PaymentType = "Direct debit"
    };
}
