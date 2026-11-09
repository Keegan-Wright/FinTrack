using FinanceTracker.Infrastructure.Notifications.Webhooks;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications.Webhooks;

public class WebhookHttpTests
{
    [Test]
    public async Task Post_ToThisMachineWhenRefused_IsRefusedBeforeConnecting()
    {
        // Arrange, nothing listens there, so only a refusal explains the failure
        using HttpClient client = new(WebhookHttp.CreateHandler(new WebhookSettings()));

        // Act, Assert
        HttpRequestException? exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.PostAsync("http://127.0.0.1:9/hook", new StringContent("{}")));
        await Assert.That(WebhookFailure.Describe(exception!)).IsEqualTo(WebhookAddresses.PrivateRefusal);
    }

    [Test]
    public async Task Post_ToThisMachineWhenAllowed_TriesToConnect()
    {
        // Arrange
        using HttpClient client = new(WebhookHttp.CreateHandler(new WebhookSettings { AllowPrivateAddresses = true }));

        // Act, Assert, nothing listens on the discard port, so it can't be reached rather than refused
        HttpRequestException? exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.PostAsync("http://127.0.0.1:9/hook", new StringContent("{}")));
        await Assert.That(WebhookFailure.Describe(exception!)).IsEqualTo("FinTrack couldn't reach the webhook.");
    }
}
