using FinanceTracker.Infrastructure.Persistence;

namespace FinanceTracker.Tests.Unit.Infrastructure.Persistence;

public class DayBlindIndexTests
{
    [Test]
    public async Task Value_IsTheUtcDay()
    {
        // Act
        string late = DayBlindIndex.Value(new DateTime(2026, 10, 14, 23, 59, 59, DateTimeKind.Utc));
        string early = DayBlindIndex.Value(new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(late).IsEqualTo("day:2026-10-14");
            await Assert.That(early).IsEqualTo(late);
        }
    }

    [Test]
    public async Task Value_UnspecifiedKind_IsTakenAsUtc()
    {
        // Act
        string value = DayBlindIndex.Value(new DateTime(2026, 10, 14, 23, 30, 0, DateTimeKind.Unspecified));

        // Assert
        await Assert.That(value).IsEqualTo("day:2026-10-14");
    }

    [Test]
    public async Task Values_CoverEveryDayOfTheRangeAndOneEitherSide()
    {
        // Act
        List<string>? values = DayBlindIndex.Values(new DateTime(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 16, 18, 0, 0, DateTimeKind.Utc));

        // Assert
        await Assert.That(values!).IsEquivalentTo([
            "day:2026-10-13", "day:2026-10-14", "day:2026-10-15", "day:2026-10-16", "day:2026-10-17"
        ]);
    }

    [Test]
    public async Task Values_RangeTooLong_IsNull()
    {
        // Arrange
        DateTime from = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        List<string>? values = DayBlindIndex.Values(from, from.AddDays(DayBlindIndex.MaxDays));

        // Assert
        await Assert.That(values).IsNull();
    }

    [Test]
    public async Task Values_Backwards_IsNull()
    {
        // Arrange
        DateTime from = new(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc);

        // Act
        List<string>? values = DayBlindIndex.Values(from, from.AddDays(-5));

        // Assert
        await Assert.That(values).IsNull();
    }

    [Test]
    public async Task ValuesAfter_AreTheDaysPastTheMarginUpToAWeekFromNow()
    {
        // Arrange
        DateTime now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

        // Act
        List<string> values = DayBlindIndex.ValuesAfter(now.AddDays(-2), now);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(values).DoesNotContain("day:2026-10-27");
            await Assert.That(values).Contains("day:2026-10-28");
            await Assert.That(values).Contains("day:2026-11-04");
            await Assert.That(values).Count().IsEqualTo(8);
        }
    }

    [Test]
    public async Task ValuesAfter_LongAgo_IsTheLatestMaxDays()
    {
        // Arrange
        DateTime now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

        // Act
        List<string> values = DayBlindIndex.ValuesAfter(now.AddYears(-10), now);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(values).Count().IsEqualTo(DayBlindIndex.MaxDays);
            await Assert.That(values).Contains("day:2026-11-04");
        }
    }

    [Test]
    public async Task ValuesAfter_AlreadyPastAWeekFromNow_IsEmpty()
    {
        // Arrange
        DateTime now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

        // Act
        List<string> values = DayBlindIndex.ValuesAfter(now.AddDays(30), now);

        // Assert
        await Assert.That(values).IsEmpty();
    }
}
