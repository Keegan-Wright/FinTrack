using System.Net;
using System.Text.Json;
using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Integration.Api;

/// <summary>
/// Push notifications through the API, with a test push caught on this machine and read as the browser would.
/// </summary>
public class PushApiTests : IntegrationTestBase
{
    [Test]
    public async Task TestPush_ThroughTheApi_ArrivesEncryptedForTheBrowserAndSigned()
    {
        // Arrange
        using WebhookCatcher pushService = new(HttpStatusCode.Created);
        using WebPushTestBrowser browser = new();
        SignedInUser user = await RegisterUserAsync();
        IPushService service = ApiFor(user).Get<IPushService>();
        PushDeviceAdded added = await service.AddDeviceAsync(Subscription(pushService, browser), CancellationToken);

        // Act
        PushSettings? settings = await service.SendTestAsync(CancellationToken);

        // Assert, read as the browser would
        CaughtPost post = await pushService.NextAsync(CancellationToken);
        using JsonDocument message = JsonDocument.Parse(browser.Decrypt(post.Body));
        using (Assert.Multiple())
        {
            await Assert.That(message.RootElement.GetProperty("title").GetString()).IsEqualTo("FinTrack");
            await Assert.That(post.Headers["Content-Encoding"]).IsEqualTo("aes128gcm");
            await Assert.That(post.Headers["Authorization"]).StartsWith("vapid t=");
            await Assert.That(post.Headers["Authorization"]).EndsWith($"k={added.Settings.PublicKey}");
            await Assert.That(settings!.Devices.Single().LastPush!.Sent).IsTrue();
        }
    }

    [Test]
    public async Task TestPush_BrowserHasGone_RemovesTheDevice()
    {
        // Arrange
        using WebhookCatcher pushService = new(HttpStatusCode.Gone);
        using WebPushTestBrowser browser = new();
        SignedInUser user = await RegisterUserAsync();
        IPushService service = ApiFor(user).Get<IPushService>();
        await service.AddDeviceAsync(Subscription(pushService, browser), CancellationToken);

        // Act
        PushSettings? settings = await service.SendTestAsync(CancellationToken);

        // Assert
        await Assert.That(settings!.Devices).IsEmpty();
    }

    [Test]
    public async Task AddDevice_BrowserAnotherUserHad_MovesToTheNewUser()
    {
        // Arrange, one browser, two people signing in on it in turn
        using WebhookCatcher pushService = new(HttpStatusCode.Created);
        using WebPushTestBrowser browser = new();
        SignedInUser first = await RegisterUserAsync();
        SignedInUser second = await RegisterUserAsync();
        await ApiFor(first).Get<IPushService>().AddDeviceAsync(Subscription(pushService, browser), CancellationToken);

        // Act
        await ApiFor(second).Get<IPushService>().AddDeviceAsync(Subscription(pushService, browser), CancellationToken);

        // Assert
        PushSettings firsts = await ApiFor(first).Get<IPushService>().GetSettingsAsync(CancellationToken);
        PushSettings seconds = await ApiFor(second).Get<IPushService>().GetSettingsAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(firsts.Devices).IsEmpty();
            await Assert.That(seconds.Devices).Count().IsEqualTo(1);
        }
    }

    [Test]
    public async Task RemoveDevice_SomeoneElses_Is404()
    {
        // Arrange
        using WebhookCatcher pushService = new(HttpStatusCode.Created);
        using WebPushTestBrowser browser = new();
        SignedInUser owner = await RegisterUserAsync();
        SignedInUser other = await RegisterUserAsync();
        PushDeviceAdded added = await ApiFor(owner).Get<IPushService>()
            .AddDeviceAsync(Subscription(pushService, browser), CancellationToken);

        // Act
        PushSettings? removed = await ApiFor(other).Get<IPushService>()
            .RemoveDeviceAsync(added.DeviceId, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(removed).IsNull();
            await Assert.That((await ApiFor(owner).Get<IPushService>().GetSettingsAsync(CancellationToken)).Devices)
                .Count().IsEqualTo(1);
        }
    }

    [Test]
    public async Task LockScreen_AsTheyStart_HidesAmounts_AndChangingItIsKept()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPushService service = ApiFor(user).Get<IPushService>();

        // Act
        PushSettings atFirst = await service.GetSettingsAsync(CancellationToken);
        await service.ChangeLockScreenAsync(new ChangeLockScreen { ShowAmounts = true }, CancellationToken);
        PushSettings after = await ApiFor(user).Get<IPushService>().GetSettingsAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(atFirst.ShowAmountsOnLockScreen).IsFalse();
            await Assert.That(after.ShowAmountsOnLockScreen).IsTrue();
        }
    }

    [Test]
    public async Task TestPush_NoDevices_Is409()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();

        // Act
        PushSettings? settings = await ApiFor(user).Get<IPushService>().SendTestAsync(CancellationToken);

        // Assert
        await Assert.That(settings).IsNull();
    }

    [Test]
    public async Task AddDevice_NotWhatABrowserGives_IsABadRequest()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPushService service = ApiFor(user).Get<IPushService>();

        // Act
        HttpRequestException? exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.AddDeviceAsync(new AddPushDevice
            {
                Endpoint = "https://push.example.test/1", PublicKey = "short", AuthSecret = "x", Name = "Chrome"
            }, CancellationToken));

        // Assert
        await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    private static AddPushDevice Subscription(WebhookCatcher pushService, WebPushTestBrowser browser) => new()
    {
        Endpoint = pushService.Url.ToString(),
        PublicKey = browser.PublicKeyText,
        AuthSecret = browser.AuthSecretText,
        Name = "Chrome on Android"
    };
}
