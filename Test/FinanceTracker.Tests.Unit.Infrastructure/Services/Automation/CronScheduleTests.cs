using FinanceTracker.Infrastructure.Automation;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Automation;

public class CronScheduleTests
{
    private static readonly DateTime After = new(2026, 3, 10, 12, 0, 30, DateTimeKind.Utc);

    [Test]
    [Arguments("0 */15 * * * *", 12, 15, 0)]
    [Arguments("*/15 * * * *", 12, 15, 0)]
    [Arguments("45 0 12 * * *", 12, 0, 45)]
    public async Task NextAfter_ReadsSixPartsWithSecondsAndTheUsualFive(string expression, int hour, int minute,
        int second)
    {
        // Act
        DateTime? next = CronSchedule.NextAfter(expression, After);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(next).IsEqualTo(new DateTime(2026, 3, 10, hour, minute, second, DateTimeKind.Utc));
            await Assert.That(next?.Kind).IsEqualTo(DateTimeKind.Utc);
        }
    }

    [Test]
    public async Task NextAfter_ForAnExpressionItCantRead_IsNull()
    {
        // Act
        DateTime? next = CronSchedule.NextAfter("every so often", After);

        // Assert
        await Assert.That(next).IsNull();
    }
}
