using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications.Push;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using DomainPushDevice = FinanceTracker.Domain.Notifications.PushDevice;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications.Push;

public class PushServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";
    private const string Endpoint = "https://push.example.test/send/one";

    private static readonly DateTime Now = new(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);

    private readonly IPushSender _pushSender = Substitute.For<IPushSender>();
    private readonly WebPushTestBrowser _browser = new();
    private VapidKeyStore _keyStore = null!;

    [Before(Test)]
    public async Task SetUpAsync()
    {
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername));
        _keyStore = new VapidKeyStore(DbContextFactory, new FixedTimeProvider(Now));
    }

    [After(Test)]
    public void TearDown()
    {
        _browser.Dispose();
        _keyStore.Dispose();
    }

    [Test]
    public async Task GetSettings_GivesTheServersPublicKey_TheSameEachTime()
    {
        // Act
        PushSettings first = await Subject(Username).GetSettingsAsync(CancellationToken);
        PushSettings second = await Subject(OtherUsername).GetSettingsAsync(CancellationToken);

        // Assert, an uncompressed P-256 key
        using (Assert.Multiple())
        {
            await Assert.That(System.Buffers.Text.Base64Url.DecodeFromChars(first.PublicKey).Length).IsEqualTo(65);
            await Assert.That(second.PublicKey).IsEqualTo(first.PublicKey);
            await Assert.That(first.Devices).IsEmpty();
        }
    }

    [Test]
    public async Task VapidKeys_AnotherServer_ReadsTheSavedPair()
    {
        // Arrange
        VapidKeys saved = await _keyStore.GetAsync(CancellationToken);
        using VapidKeyStore otherServer = new(DbContextFactory, new FixedTimeProvider(Now));

        // Act
        VapidKeys read = await otherServer.GetAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(read.PublicKey).IsEquivalentTo(saved.PublicKey);
            await Assert.That(read.PrivateKey).IsEquivalentTo(saved.PrivateKey);
        }
    }

    [Test]
    public async Task AddDevice_New_ListsItForThemOnly()
    {
        // Act
        PushDeviceAdded added = await Subject(Username).AddDeviceAsync(Subscription("Chrome on Android"),
            CancellationToken);
        PushSettings others = await Subject(OtherUsername).GetSettingsAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(added.Settings.Devices.Select(static x => x.Name)).IsEquivalentTo(["Chrome on Android"]);
            await Assert.That(added.Settings.Devices.Single().Id).IsEqualTo(added.DeviceId);
            await Assert.That(others.Devices).IsEmpty();
        }
    }

    [Test]
    public async Task AddDevice_SameBrowserAgain_UpdatesItRatherThanAddingAnother()
    {
        // Arrange
        PushDeviceAdded first = await Subject(Username).AddDeviceAsync(Subscription("Chrome on Android"),
            CancellationToken);

        // Act
        PushDeviceAdded again = await Subject(Username).AddDeviceAsync(Subscription("Chrome on Pixel"),
            CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(again.DeviceId).IsEqualTo(first.DeviceId);
            await Assert.That(again.Settings.Devices.Select(static x => x.Name)).IsEquivalentTo(["Chrome on Pixel"]);
        }
    }

    [Test]
    public async Task AddDevice_BrowserAnotherUserAdded_IsTheirsNoMore()
    {
        // Arrange, someone else signed in on this browser before
        await Subject(OtherUsername).AddDeviceAsync(Subscription("Shared laptop"), CancellationToken);

        // Act
        PushDeviceAdded added = await Subject(Username).AddDeviceAsync(Subscription("Shared laptop"),
            CancellationToken);
        PushSettings others = await Subject(OtherUsername).GetSettingsAsync(CancellationToken);

        // Assert, so their notifications don't pop up for whoever uses it now
        using (Assert.Multiple())
        {
            await Assert.That(added.Settings.Devices).Count().IsEqualTo(1);
            await Assert.That(others.Devices).IsEmpty();
        }
    }

    [Test]
    public async Task AddDevice_KeepsTheEndpointAndKeysEncrypted()
    {
        // Act
        await Subject(Username).AddDeviceAsync(Subscription("Chrome on Android"), CancellationToken);

        // Assert, nothing stored as given
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        List<string> stored = await context.Database
            .SqlQueryRaw<string>("""SELECT "Endpoint" || "PublicKey" || "AuthSecret" AS "Value" FROM "PushDevices" """)
            .ToListAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(stored.Single()).DoesNotContain(Endpoint);
            await Assert.That(stored.Single()).DoesNotContain(_browser.PublicKeyText);
            await Assert.That(stored.Single()).DoesNotContain(_browser.AuthSecretText);
        }
    }

    [Test]
    public async Task RemoveDevice_Theirs_StopsListingIt()
    {
        // Arrange
        PushDeviceAdded added = await Subject(Username).AddDeviceAsync(Subscription("Chrome on Android"),
            CancellationToken);

        // Act
        PushSettings? settings = await Subject(Username).RemoveDeviceAsync(added.DeviceId, CancellationToken);

        // Assert
        await Assert.That(settings!.Devices).IsEmpty();
    }

    [Test]
    public async Task RemoveDevice_SomeoneElses_IsNotFound_AndStays()
    {
        // Arrange
        PushDeviceAdded added = await Subject(OtherUsername).AddDeviceAsync(Subscription("Chrome on Android"),
            CancellationToken);

        // Act
        PushSettings? settings = await Subject(Username).RemoveDeviceAsync(added.DeviceId, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings).IsNull();
            await Assert.That((await Subject(OtherUsername).GetSettingsAsync(CancellationToken)).Devices).Count()
                .IsEqualTo(1);
        }
    }

    [Test]
    public async Task SendTest_NoDevices_IsNothingToDo()
    {
        // Act
        PushSettings? settings = await Subject(Username).SendTestAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings).IsNull();
            await _pushSender.DidNotReceive().SendAsync(Arg.Any<DomainPushDevice>(), Arg.Any<PushMessage>(),
                Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task SendTest_Delivered_RecordsIt()
    {
        // Arrange
        await Subject(Username).AddDeviceAsync(Subscription("Chrome on Android"), CancellationToken);
        _pushSender.SendAsync(Arg.Any<DomainPushDevice>(), PushMessage.Test, Arg.Any<CancellationToken>())
            .Returns(PushOutcome.Delivered);

        // Act
        PushSettings? settings = await Subject(Username).SendTestAsync(CancellationToken);

        // Assert
        DeliveryStatus? last = settings!.Devices.Single().LastPush;
        using (Assert.Multiple())
        {
            await Assert.That(last!.At).IsEqualTo(Now);
            await Assert.That(last.Sent).IsTrue();
        }
    }

    [Test]
    public async Task SendTest_BrowserGone_RemovesTheDevice()
    {
        // Arrange
        await Subject(Username).AddDeviceAsync(Subscription("Chrome on Android"), CancellationToken);
        _pushSender.SendAsync(Arg.Any<DomainPushDevice>(), PushMessage.Test, Arg.Any<CancellationToken>())
            .Returns(PushOutcome.Gone);

        // Act
        PushSettings? settings = await Subject(Username).SendTestAsync(CancellationToken);

        // Assert
        await Assert.That(settings!.Devices).IsEmpty();
    }

    [Test]
    public async Task SendTest_Fails_RecordsWhyInWords()
    {
        // Arrange
        await Subject(Username).AddDeviceAsync(Subscription("Chrome on Android"), CancellationToken);
        _pushSender.SendAsync(Arg.Any<DomainPushDevice>(), PushMessage.Test, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("refused"));

        // Act
        PushSettings? settings = await Subject(Username).SendTestAsync(CancellationToken);

        // Assert
        await Assert.That(settings!.Devices.Single().LastPush!.Failure)
            .IsEqualTo("FinTrack couldn't reach the push service.");
    }

    private PushService Subject(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<PushService>.Instance, _keyStore, _pushSender,
            new FixedTimeProvider(Now));

    private AddPushDevice Subscription(string name) => new()
    {
        Endpoint = Endpoint,
        PublicKey = _browser.PublicKeyText,
        AuthSecret = _browser.AuthSecretText,
        Name = name
    };
}
