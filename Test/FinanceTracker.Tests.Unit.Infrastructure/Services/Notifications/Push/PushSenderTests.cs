using System.Net;
using System.Text.Json;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Notifications.Push;
using FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications.Webhooks;
using FinanceTracker.Tests.Unit.Shared;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications.Push;

public class PushSenderTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);
    private readonly VapidKeys _keys = VapidKeys.Create();

    [Test]
    public async Task Send_PostsTheMessageEncryptedSoOnlyTheBrowserReadsIt()
    {
        // Arrange
        using WebPushTestBrowser browser = new();
        RecordingWebhookHandler handler = new(HttpStatusCode.Created);

        // Act
        PushOutcome outcome = await Send(handler, browser, PushMessage.Test);

        // Assert
        using JsonDocument message = JsonDocument.Parse(browser.Decrypt(handler.Body));
        using (Assert.Multiple())
        {
            await Assert.That(outcome).IsEqualTo(PushOutcome.Delivered);
            await Assert.That(handler.Request!.Method).IsEqualTo(HttpMethod.Post);
            await Assert.That(handler.Request.RequestUri).IsEqualTo(new Uri("https://push.example.test/send/1"));
            await Assert.That(message.RootElement.GetProperty("title").GetString()).IsEqualTo("FinTrack");
            await Assert.That(message.RootElement.GetProperty("path").GetString())
                .IsEqualTo("Notifications/Settings");
        }
    }

    [Test]
    public async Task Send_HasThePushHeaders()
    {
        // Arrange
        using WebPushTestBrowser browser = new();
        RecordingWebhookHandler handler = new(HttpStatusCode.Created);

        // Act
        await Send(handler, browser, PushMessage.Test);

        // Assert
        HttpRequestMessage request = handler.Request!;
        using (Assert.Multiple())
        {
            await Assert.That(request.Content!.Headers.ContentEncoding).IsEquivalentTo(["aes128gcm"]);
            await Assert.That(request.Content.Headers.ContentType!.MediaType).IsEqualTo("application/octet-stream");
            await Assert.That(request.Headers.GetValues("TTL").Single()).IsEqualTo("86400");
            await Assert.That(request.Headers.GetValues("Urgency").Single()).IsEqualTo("normal");
            await Assert.That(request.Headers.Authorization!.Scheme).IsEqualTo("vapid");
            await Assert.That(request.Headers.Authorization.Parameter).EndsWith($", k={_keys.PublicKeyText}");
        }
    }

    [Test]
    [Arguments(HttpStatusCode.NotFound)]
    [Arguments(HttpStatusCode.Gone)]
    public async Task Send_BrowserHasUnsubscribed_SaysItsGone(HttpStatusCode status)
    {
        // Arrange
        using WebPushTestBrowser browser = new();
        RecordingWebhookHandler handler = new(status);

        // Act
        PushOutcome outcome = await Send(handler, browser, PushMessage.Test);

        // Assert
        await Assert.That(outcome).IsEqualTo(PushOutcome.Gone);
    }

    [Test]
    public async Task Send_AnswerIsNotSuccess_SaysWhatItAnswered()
    {
        // Arrange
        using WebPushTestBrowser browser = new();
        RecordingWebhookHandler handler = new(HttpStatusCode.TooManyRequests);

        // Act, Assert
        PushRejectedException? rejected = await Assert.ThrowsAsync<PushRejectedException>(() =>
            Send(handler, browser, PushMessage.Test));
        await Assert.That(PushFailure.Describe(rejected!)).IsEqualTo("The push service answered 429, not success.");
    }

    private async Task<PushOutcome> Send(RecordingWebhookHandler handler, WebPushTestBrowser browser,
        PushMessage message)
    {
        IVapidKeyStore keyStore = Substitute.For<IVapidKeyStore>();
        keyStore.GetAsync(Arg.Any<CancellationToken>()).Returns(_keys);
        PushSender sender = new(new HandlerClientFactory(handler), keyStore,
            new PublicAddress(new Uri("https://fintrack.example.test/")), new FixedTimeProvider(Now));
        PushDevice device = new(Guid.NewGuid(), "https://push.example.test/send/1", browser.PublicKeyText,
            browser.AuthSecretText, "Chrome on Android");
        return await sender.SendAsync(device, message, CancellationToken.None);
    }
}
