using FinanceTracker.Client.Components.Automation;
using FinanceTracker.Contracts.Automation;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.Automation;

public class AutomationTextTests
{
    internal const string SyncFunction = "SyncAllOpenBankingDetailsAsync";
    internal const string UserSyncFunction = "SyncUserOpenBankingDetailsAsync";

    [Test]
    [Arguments(SyncFunction, "Sync all open banking details")]
    [Arguments("MoveEventSavingsGoalsOnAsync", "Move event savings goals on")]
    [Arguments("Cleanup", "Cleanup")]
    public async Task JobName_TurnsTheFunctionIntoWords(string function, string expected)
    {
        // Act
        string name = AutomationText.JobName(function);

        // Assert
        await Assert.That(name).IsEqualTo(expected);
    }

    [Test]
    [Arguments(4500, "4.5s")]
    [Arguments(130_000, "2m 10s")]
    [Arguments(3_900_000, "1h 5m")]
    public async Task Duration_UsesTheNearestUsefulUnit(int milliseconds, string expected)
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        string duration = AutomationText.Duration(TimeSpan.FromMilliseconds(milliseconds));

        // Assert
        await Assert.That(duration).IsEqualTo(expected);
    }

    [Test]
    public async Task NextRun_SaysWhenAJobWontRunOnASchedule()
    {
        // Arrange
        using CultureScope culture = new();
        AutomationJobSummary scheduled = Job(nextRunAt: new DateTime(2026, 11, 3, 14, 5, 0));
        AutomationJobSummary paused = Job(enabled: false);
        AutomationJobSummary byHand = Job(scheduled: false);

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(AutomationText.NextRun(scheduled)).IsEqualTo("3 Nov 14:05");
            await Assert.That(AutomationText.NextRun(paused)).IsEqualTo("Not while it's paused");
            await Assert.That(AutomationText.NextRun(byHand)).IsEqualTo("Only runs when asked to");
        }
    }

    [Test]
    public async Task SuccessRate_SaysHowManyRunsItCovers()
    {
        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(AutomationText.SuccessRate(Job(successRate: 95, recentRuns: 20)))
                .IsEqualTo("95% of the last 20 runs");
            await Assert.That(AutomationText.SuccessRate(Job(successRate: 100, recentRuns: 1)))
                .IsEqualTo("100% of the last run");
            await Assert.That(AutomationText.SuccessRate(Job())).IsEqualTo("No finished runs yet");
        }
    }

    [Test]
    [Arguments(100, Color.Default)]
    [Arguments(80, Color.Warning)]
    [Arguments(40, Color.Error)]
    public async Task SuccessRateColor_WarnsOnceARecentRunFailed(int successRate, Color expected)
    {
        // Act
        Color color = AutomationText.SuccessRateColor(Job(successRate: successRate, recentRuns: 20));

        // Assert
        await Assert.That(color).IsEqualTo(expected);
    }

    [Test]
    public async Task RunWhen_AddsHowLongAFinishedRunTook_AndSaysWhenOneNotStartedIsDue()
    {
        // Arrange
        using CultureScope culture = new();
        DateTime due = new(2026, 11, 3, 14, 0, 0);
        AutomationRun finished = Run(AutomationRunStatus.Done, due, startedAt: due.AddSeconds(2),
            duration: TimeSpan.FromSeconds(4.5));
        AutomationRun waiting = Run(AutomationRunStatus.Waiting, due);

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(AutomationText.RunWhen(finished)).IsEqualTo("3 Nov 14:00, took 4.5s");
            await Assert.That(AutomationText.RunWhen(waiting)).IsEqualTo("Due 3 Nov 14:00");
        }
    }

    [Test]
    [Arguments(0, null)]
    [Arguments(1, "Retried once")]
    [Arguments(3, "Retried 3 times")]
    public async Task Retries_OnlySaysSoWhenARunWasRetried(int retryCount, string? expected)
    {
        // Act
        string? retries = AutomationText.Retries(Run(AutomationRunStatus.Done, DateTime.Now, retryCount: retryCount));

        // Assert
        await Assert.That(retries).IsEqualTo(expected);
    }

    internal static AutomationJobSummary Job(string function = SyncFunction, bool scheduled = true,
        bool enabled = true, DateTime? nextRunAt = null, AutomationRun? lastRun = null, int? successRate = null,
        int recentRuns = 0)
    {
        AutomationCron? schedule = null;
        if (scheduled)
        {
            schedule = new AutomationCron(Guid.NewGuid(), function, $"{function} description", "0 */15 * * * *", 2,
                [30, 60], enabled);
        }

        return new AutomationJobSummary(function, schedule, nextRunAt, lastRun, successRate, recentRuns);
    }

    internal static AutomationRun Run(AutomationRunStatus status, DateTime dueAt, string function = SyncFunction,
        bool isManual = false, DateTime? startedAt = null, TimeSpan? duration = null, int retryCount = 0,
        string? error = null) =>
        new(Guid.NewGuid(), function, status, isManual, dueAt, startedAt, duration, retryCount, error);
}
