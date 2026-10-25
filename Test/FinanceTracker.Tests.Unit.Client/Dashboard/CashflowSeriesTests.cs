using FinanceTracker.Client.Components.Dashboard;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Tests.Unit.Client.Dashboard;

public class CashflowSeriesTests
{
    [Test]
    public async Task Build_ForAShortRange_HasAPointADayWithARunningNet()
    {
        // Arrange
        using CultureScope culture = new();
        Transaction[] transactions =
        [
            Transaction(100m, new DateTime(2026, 8, 1, 9, 0, 0)),
            Transaction(-30m, new DateTime(2026, 8, 3, 12, 0, 0)),
            Transaction(-20m, new DateTime(2026, 8, 3, 18, 0, 0))
        ];

        // Act
        List<CashflowPoint> points = CashflowSeries.Build(transactions, new DateTime(2026, 8, 1), new DateTime(2026, 8, 3));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(points.Select(static x => x.Label)).IsEquivalentTo(["1 Aug", "2 Aug", "3 Aug"]);
            await Assert.That(points[0].In).IsEqualTo(100m);
            await Assert.That(points[1].RunningNet).IsEqualTo(100m);
            await Assert.That(points[2].Out).IsEqualTo(50m);
            await Assert.That(points[2].RunningNet).IsEqualTo(50m);
        }
    }

    [Test]
    public async Task Build_ForMoreThanTwoMonths_HasAPointAMonth()
    {
        // Arrange
        using CultureScope culture = new();
        DateTime from = new(2026, 1, 1);
        DateTime to = new(2026, 3, 31);
        Transaction[] transactions = [Transaction(10m, new DateTime(2026, 2, 14, 9, 0, 0))];

        // Act
        List<CashflowPoint> points = CashflowSeries.Build(transactions, from, to);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(CashflowSeries.IsByMonth(from, to)).IsTrue();
            await Assert.That(points.Select(static x => x.Label)).IsEquivalentTo(["Jan", "Feb", "Mar"]);
            await Assert.That(points[1].In).IsEqualTo(10m);
        }
    }

    [Test]
    public async Task IsByMonth_UpToSixtyTwoDays_IsDaily()
    {
        // Act
        bool byMonth = CashflowSeries.IsByMonth(new DateTime(2026, 7, 1), new DateTime(2026, 8, 31));

        // Assert
        await Assert.That(byMonth).IsFalse();
    }

    [Test]
    public async Task Build_LeavesOutTransactionsOutsideTheRange()
    {
        // Arrange
        Transaction[] transactions =
        [
            Transaction(10m, new DateTime(2026, 7, 31, 23, 0, 0)),
            Transaction(5m, new DateTime(2026, 8, 1, 9, 0, 0))
        ];

        // Act
        List<CashflowPoint> points = CashflowSeries.Build(transactions, new DateTime(2026, 8, 1), new DateTime(2026, 8, 1));

        // Assert
        await Assert.That(points.Single().In).IsEqualTo(5m);
    }

    [Test]
    public async Task Build_WithNoTransactions_IsEmpty()
    {
        // Act
        List<CashflowPoint> points = CashflowSeries.Build([], new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));

        // Assert
        await Assert.That(points).IsEmpty();
    }

    // Local times, so bucketing by local day doesn't depend on the machine's time zone
    private static Transaction Transaction(decimal amount, DateTime localTime) => new()
    {
        TransactionId = Guid.NewGuid(),
        Description = "Test",
        TransactionType = amount < 0 ? "Debit" : "Credit",
        TransactionCategory = "PURCHASE",
        Amount = amount,
        Currency = "GBP",
        TransactionTime = DateTime.SpecifyKind(localTime, DateTimeKind.Local),
        Pending = false,
        Tags = []
    };
}
