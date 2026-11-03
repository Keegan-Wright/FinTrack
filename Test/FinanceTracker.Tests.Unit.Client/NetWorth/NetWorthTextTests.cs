using FinanceTracker.Client.Components.NetWorth;
using FinanceTracker.Contracts.NetWorth;

namespace FinanceTracker.Tests.Unit.Client.NetWorth;

public class NetWorthTextTests
{
    [Test]
    public async Task ChartPoints_SplitsWorkedOutDaysFromSyncedAndJoinsThem()
    {
        // Arrange
        BalancePoint[] points =
        [
            Point(1, 100m, derived: true), Point(2, 110m, derived: true), Point(3, 120m), Point(4, 130m)
        ];

        // Act
        IReadOnlyList<HistoryChartPoint> chart = NetWorthText.ChartPoints(points);

        // Assert, the 3rd is in both so the lines meet
        await Assert.That(chart.Select(static x => (x.Synced, x.Derived))).IsEquivalentTo(
            [((decimal?)null, (decimal?)100m), (null, 110m), (120m, 120m), (130m, null)],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(120, "Up £120.00 this month")]
    [Arguments(-35.5, "Down £35.50 this month")]
    [Arguments(0, "No change this month")]
    public async Task Change_SaysWhichWayAndHowMuch(double change, string expected)
    {
        // Arrange
        using CultureScope culture = new();

        // Act & Assert
        await Assert.That(NetWorthText.Change((decimal)change, "month")).IsEqualTo(expected);
    }

    [Test]
    public async Task Change_NoEarlierHistory_SaysSo() =>
        await Assert.That(NetWorthText.Change(null, "year")).IsEqualTo("No history from before this year");

    [Test]
    public async Task ChangeAcross_ComparesTheLastDayWithTheFirst()
    {
        // Arrange
        using CultureScope culture = new();
        NetWorthPoint[] points =
        [
            new() { Date = new DateOnly(2026, 11, 1), NetWorth = 1000m },
            new() { Date = new DateOnly(2026, 11, 2), NetWorth = 900m },
            new() { Date = new DateOnly(2026, 11, 3), NetWorth = 1120m }
        ];

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(NetWorthText.ChangeAcross(points)).IsEqualTo("Up £120.00 since 1 Nov");
            await Assert.That(NetWorthText.ChangeAcross([points[0]])).IsNull();
        }
    }

    [Test]
    public async Task DerivedNote_SaysFromWhenItsSynced()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        string? note = NetWorthText.DerivedNote([Point(1, 1m, derived: true), Point(2, 1m)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(note).IsEqualTo(
                "The dashed line, before 2 Nov 2026, is worked out from your transactions, so approximate");
            await Assert.That(NetWorthText.DerivedNote([Point(1, 1m)])).IsNull();
        }
    }

    internal static BalancePoint Point(int day, decimal amount, bool derived = false) =>
        new() { Date = new DateOnly(2026, 10, 31).AddDays(day), Amount = amount, IsDerived = derived };
}
