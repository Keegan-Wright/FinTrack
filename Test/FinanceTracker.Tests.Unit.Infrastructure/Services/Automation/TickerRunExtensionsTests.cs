using FinanceTracker.Contracts.Automation;
using FinanceTracker.Infrastructure.Automation;
using TickerQ.Utilities.Enums;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Automation;

public class TickerRunExtensionsTests
{
    [Test]
    [Arguments(TickerStatus.Idle, 0, AutomationRunStatus.Waiting)]
    [Arguments(TickerStatus.Queued, 0, AutomationRunStatus.Waiting)]
    [Arguments(TickerStatus.Queued, 1, AutomationRunStatus.Retrying)]
    [Arguments(TickerStatus.InProgress, 0, AutomationRunStatus.Running)]
    [Arguments(TickerStatus.InProgress, 2, AutomationRunStatus.Retrying)]
    [Arguments(TickerStatus.Done, 0, AutomationRunStatus.Done)]
    [Arguments(TickerStatus.DueDone, 1, AutomationRunStatus.Done)]
    [Arguments(TickerStatus.Failed, 3, AutomationRunStatus.Failed)]
    [Arguments(TickerStatus.Cancelled, 0, AutomationRunStatus.Cancelled)]
    [Arguments(TickerStatus.Skipped, 0, AutomationRunStatus.Skipped)]
    public async Task ToRunStatus_CountsAnUnfinishedRunThatHasFailedBeforeAsARetry(TickerStatus status, int retryCount,
        AutomationRunStatus expected)
    {
        // Act
        AutomationRunStatus runStatus = status.ToRunStatus(retryCount);

        // Assert
        await Assert.That(runStatus).IsEqualTo(expected);
    }
}
