using FinanceTracker.Client.Api;
using Microsoft.AspNetCore.SignalR.Client;

namespace FinanceTracker.Tests.Unit.Client.Api;

public class KeepTryingRetryPolicyTests
{
    [Test]
    [Arguments(0, 0)]
    [Arguments(1, 2)]
    [Arguments(2, 10)]
    [Arguments(3, 30)]
    [Arguments(4, 30)]
    [Arguments(1_000, 30)]
    public async Task NextRetryDelay_AfterFailedTries_WaitsLongerUpToItsLongestAndNeverGivesUp(long failedTries,
        int seconds)
    {
        // Arrange
        KeepTryingRetryPolicy policy = new();
        RetryContext context = new() { PreviousRetryCount = failedTries };

        // Act
        TimeSpan? wait = policy.NextRetryDelay(context);

        // Assert
        await Assert.That(wait).IsEqualTo(TimeSpan.FromSeconds(seconds));
    }
}
