using FinanceTracker.Client.Components.Transactions;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.Transactions;

public class DateRangePresetsTests
{
    [Test]
    public async Task For_InJanuary_LastMonthIsTheDecemberBefore()
    {
        // Act
        DateRange lastMonth = Preset(new DateTime(2026, 1, 15), "Last month");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(lastMonth.Start).IsEqualTo(new DateTime(2025, 12, 1));
            await Assert.That(lastMonth.End).IsEqualTo(new DateTime(2025, 12, 31));
        }
    }

    [Test]
    public async Task For_ThisMonthAndThisYear_RunToToday()
    {
        // Arrange
        DateTime today = new(2026, 10, 25, 18, 30, 0);

        // Act
        DateRange thisMonth = Preset(today, "This month");
        DateRange thisYear = Preset(today, "This year");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(thisMonth.Start).IsEqualTo(new DateTime(2026, 10, 1));
            await Assert.That(thisMonth.End).IsEqualTo(new DateTime(2026, 10, 25));
            await Assert.That(thisYear.Start).IsEqualTo(new DateTime(2026, 1, 1));
            await Assert.That(thisYear.End).IsEqualTo(new DateTime(2026, 10, 25));
        }
    }

    [Test]
    public async Task For_LastThreeMonths_StartsThreeMonthsBeforeToday()
    {
        // Act
        DateRange range = Preset(new DateTime(2026, 5, 31), "Last 3 months");

        // Assert
        await Assert.That(range.Start).IsEqualTo(new DateTime(2026, 2, 28));
    }

    private static DateRange Preset(DateTime today, string label) =>
        DateRangePresets.For(today).Single(x => x.Label == label).Range;
}
