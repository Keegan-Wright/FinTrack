using System.Globalization;
using System.Net;
using System.Text.Json;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Notifications.Webhooks;
using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications.Webhooks;

public class WebhookSenderTests
{
    private const string Secret = "a-test-secret";
    private static readonly DateTime Now = new(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc);

    private static readonly Notification Ended = new(Guid.NewGuid(), NotificationKind.ConsentEnded, "key",
        "Bank access has ended", "FinTrack can no longer sync Bank.", "ProviderSetup");

    [Test]
    public async Task Send_Notification_PostsItAsCamelCaseJson()
    {
        // Arrange
        RecordingWebhookHandler handler = new();

        // Act
        await Send(handler, WebhookPayload.For(Ended, new PublicAddress(new Uri("https://fintrack.example.test/"))));

        // Assert
        using JsonDocument body = JsonDocument.Parse(handler.Body);
        JsonElement root = body.RootElement;
        using (Assert.Multiple())
        {
            await Assert.That(handler.Request!.Method).IsEqualTo(HttpMethod.Post);
            await Assert.That(handler.Request.Content!.Headers.ContentType!.MediaType).IsEqualTo("application/json");
            await Assert.That(root.GetProperty("id").GetGuid()).IsEqualTo(Ended.Id);
            await Assert.That(root.GetProperty("event").GetString()).IsEqualTo("notification");
            await Assert.That(root.GetProperty("kind").GetString()).IsEqualTo("consentEnded");
            await Assert.That(root.GetProperty("title").GetString()).IsEqualTo("Bank access has ended");
            await Assert.That(root.GetProperty("message").GetString()).IsEqualTo("FinTrack can no longer sync Bank.");
            await Assert.That(root.GetProperty("link").GetString())
                .IsEqualTo("https://fintrack.example.test/ProviderSetup");
            await Assert.That(Header(handler, WebhookSender.EventHeader)).IsEqualTo("notification");
            await Assert.That(Header(handler, WebhookSender.DeliveryHeader)).IsEqualTo(Ended.Id.ToString());
        }
    }

    [Test]
    public async Task Send_Any_SignsTheTimestampAndBodyWithTheSecret()
    {
        // Arrange
        RecordingWebhookHandler handler = new();

        // Act
        await Send(handler, WebhookPayload.Test(Now, PublicAddress.Unknown));

        // Assert, as a receiver would check it
        string timestamp = Header(handler, WebhookSender.TimestampHeader);
        using (Assert.Multiple())
        {
            await Assert.That(timestamp).IsEqualTo(new DateTimeOffset(Now).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
            await Assert.That(Header(handler, WebhookSender.SignatureHeader))
                .IsEqualTo(WebhookSignature.Sign(Secret, long.Parse(timestamp, CultureInfo.InvariantCulture), handler.Body));
            await Assert.That(Header(handler, WebhookSender.EventHeader)).IsEqualTo("test");
        }
    }

    [Test]
    public async Task Send_AnswerIsNotSuccess_SaysWhatItAnswered()
    {
        // Arrange
        RecordingWebhookHandler handler = new(HttpStatusCode.NotFound);

        // Act, Assert
        WebhookRejectedException? rejected = await Assert.ThrowsAsync<WebhookRejectedException>(() =>
            Send(handler, WebhookPayload.Test(Now, PublicAddress.Unknown)));
        await Assert.That(WebhookFailure.Describe(rejected!)).IsEqualTo("The webhook answered 404, not success.");
    }

    private static async Task Send(RecordingWebhookHandler handler, WebhookPayload payload)
    {
        using WebhookSender sender = new(handler, new FixedTimeProvider(Now));
        await sender.SendAsync("https://hooks.example.test/fintrack", Secret, payload, CancellationToken.None);
    }

    private static string Header(RecordingWebhookHandler handler, string name) =>
        handler.Request!.Headers.GetValues(name).Single();
}
