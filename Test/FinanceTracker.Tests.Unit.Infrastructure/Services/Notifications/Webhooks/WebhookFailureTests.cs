using FinanceTracker.Infrastructure.Notifications.Webhooks;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications.Webhooks;

public class WebhookFailureTests
{
    [Test]
    public async Task Describe_NoAnswerInTime_SaysHowLongItWaited() =>
        await Assert.That(WebhookFailure.Describe(new TaskCanceledException("timed out", new TimeoutException())))
            .IsEqualTo("The webhook didn't answer within 10 seconds.");

    [Test]
    public async Task Describe_Unexpected_NeverRepeatsItsMessage() =>
        await Assert.That(WebhookFailure.Describe(new InvalidOperationException("internal detail")))
            .DoesNotContain("internal");
}
