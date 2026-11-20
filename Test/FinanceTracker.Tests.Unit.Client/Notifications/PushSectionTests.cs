using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Client.Components.Notifications;
using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Client.Notifications;

public class PushSectionTests : ComponentTestBase
{
    private const string PublicKey = "BPlaceholderServerKey";

    private static readonly PushDevice Phone = new()
    {
        Id = Guid.CreateVersion7(), Name = "Chrome on Android", Added = new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc)
    };

    private static readonly PushDevice Laptop = new()
    {
        Id = Guid.CreateVersion7(), Name = "Firefox on Linux", Added = new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc)
    };

    private static readonly AddPushDevice Subscription = new()
    {
        Endpoint = "https://push.example.test/send/1", PublicKey = "key", AuthSecret = "secret",
        Name = "Chrome on Android"
    };

    private readonly IPushService _pushService = Substitute.For<IPushService>();

    public PushSectionTests()
    {
        Services.AddSingleton(_pushService);
        JSInterop.Setup<string>("ftPushSupport").SetResult("supported");
        JSInterop.Setup<string>("ftPushPermission").SetResult("default");
        JSInterop.Setup<string?>("ftPushDeviceId").SetResult(null);
    }

    [Test]
    public async Task Render_NotOnThisDevice_OffersToTurnItOn_AndListsTheOthers()
    {
        // Arrange
        _pushService.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(Settings(Laptop));

        // Act
        IRenderedComponent<PushSection> cut = RenderSection();

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-push__row").Count == 1);
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("Turn on for this device");
            await Assert.That(cut.Find(".ft-push__row").TextContent).Contains("Firefox on Linux");
            await Assert.That(cut.Markup).DoesNotContain("This device<");
            await Assert.That(cut.Markup).Contains("Send a test");
        }
    }

    [Test]
    public async Task Render_ThisDeviceOn_MarksIt_AndOffersToTurnItOff()
    {
        // Arrange
        _pushService.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(Settings(Phone, Laptop));
        JSInterop.Setup<string?>("ftPushDeviceId").SetResult(Phone.Id.ToString());

        // Act
        IRenderedComponent<PushSection> cut = RenderSection();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Turn off for this device", StringComparison.Ordinal));
        AngleSharp.Dom.IElement phoneRow = cut.FindAll(".ft-push__row").Single(x => x.TextContent.Contains("Chrome"));
        await Assert.That(phoneRow.TextContent).Contains("This device");
    }

    [Test]
    public async Task Render_IPhoneNotOnTheHomeScreen_SaysToAddItFirst()
    {
        // Arrange
        _pushService.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(Settings());
        JSInterop.Setup<string>("ftPushSupport").SetResult("needs-install");

        // Act
        IRenderedComponent<PushSection> cut = RenderSection();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Add to", StringComparison.Ordinal));
        await Assert.That(cut.Markup).DoesNotContain("Turn on for this device");
    }

    [Test]
    public async Task Render_NotificationsBlocked_SaysWhereToAllowThem()
    {
        // Arrange
        _pushService.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(Settings());
        JSInterop.Setup<string>("ftPushPermission").SetResult("denied");

        // Act
        IRenderedComponent<PushSection> cut = RenderSection();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("blocked", StringComparison.Ordinal));
        await Assert.That(cut.Markup).DoesNotContain("Turn on for this device");
    }

    [Test]
    public async Task TurnOn_Allowed_AddsTheBrowser_AndRemembersIt()
    {
        // Arrange
        _pushService.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(Settings());
        _pushService.AddDeviceAsync(Arg.Any<AddPushDevice>(), Arg.Any<CancellationToken>())
            .Returns(new PushDeviceAdded { DeviceId = Phone.Id, Settings = Settings(Phone) });
        JSInterop.Setup<AddPushDevice?>("ftPushSubscribe", PublicKey).SetResult(Subscription);
        JSInterop.SetupVoid("ftRememberPushDevice", Phone.Id.ToString()).SetVoidResult();
        IRenderedComponent<PushSection> cut = RenderSection();
        cut.WaitForState(() => cut.Markup.Contains("Turn on for this device", StringComparison.Ordinal));

        // Act
        await Button(cut, "Turn on for this device").ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Turn off for this device", StringComparison.Ordinal));
        using (Assert.Multiple())
        {
            await _pushService.Received(1).AddDeviceAsync(
                Arg.Is<AddPushDevice>(x => x.Endpoint == Subscription.Endpoint), Arg.Any<CancellationToken>());
            await Assert.That(JSInterop.VerifyInvoke("ftRememberPushDevice").Arguments.Single())
                .IsEqualTo(Phone.Id.ToString());
        }
    }

    [Test]
    public async Task TurnOn_NotAllowed_AddsNothing()
    {
        // Arrange
        _pushService.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(Settings());
        JSInterop.Setup<AddPushDevice?>("ftPushSubscribe", PublicKey).SetResult(null);
        IRenderedComponent<PushSection> cut = RenderSection();
        cut.WaitForState(() => cut.Markup.Contains("Turn on for this device", StringComparison.Ordinal));
        JSInterop.Setup<string>("ftPushPermission").SetResult("denied");

        // Act
        await Button(cut, "Turn on for this device").ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("blocked", StringComparison.Ordinal));
        await _pushService.DidNotReceive().AddDeviceAsync(Arg.Any<AddPushDevice>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Remove_ThisDevice_Confirmed_RemovesIt_AndUnsubscribesTheBrowser()
    {
        // Arrange
        _pushService.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(Settings(Phone));
        _pushService.RemoveDeviceAsync(Phone.Id, Arg.Any<CancellationToken>()).Returns(Settings());
        JSInterop.Setup<string?>("ftPushDeviceId").SetResult(Phone.Id.ToString());
        JSInterop.SetupVoid("ftPushUnsubscribe").SetVoidResult();
        JSInterop.SetupVoid("ftRememberPushDevice", _ => true).SetVoidResult();
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<PushSection> cut = RenderSection();
        cut.WaitForState(() => cut.Markup.Contains("Turn off for this device", StringComparison.Ordinal));

        // Act
        Task removing = Button(cut, "Remove").ClickAsync(new());
        await RowMenu.ConfirmAsync(dialogs, "Remove");
        await removing.WaitAsync(TestTimeouts.Wait);

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-push__row").Count == 0);
        using (Assert.Multiple())
        {
            await _pushService.Received(1).RemoveDeviceAsync(Phone.Id, Arg.Any<CancellationToken>());
            await Assert.That(JSInterop.Invocations["ftPushUnsubscribe"]).Count().IsEqualTo(1);
            await Assert.That(cut.Markup).Contains("Turn on for this device");
        }
    }

    [Test]
    public async Task SendTest_ADeviceFailed_ShowsWhy()
    {
        // Arrange
        PushDevice failed = new()
        {
            Id = Laptop.Id, Name = Laptop.Name, Added = Laptop.Added,
            LastPush = new DeliveryStatus { At = Laptop.Added, Failure = "FinTrack couldn't reach the push service." }
        };
        _pushService.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(Settings(Laptop));
        _pushService.SendTestAsync(Arg.Any<CancellationToken>()).Returns(Settings(failed));
        IRenderedComponent<PushSection> cut = RenderSection();
        cut.WaitForState(() => cut.Markup.Contains("Send a test", StringComparison.Ordinal));

        // Act
        await Button(cut, "Send a test").ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("couldn't reach the push service", StringComparison.Ordinal));
        await _pushService.Received(1).SendTestAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HideAmounts_TurnedOff_SavesThatPushesSayThem()
    {
        // Arrange, hidden as they start
        _pushService.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(Settings(Phone));
        _pushService.ChangeLockScreenAsync(Arg.Any<ChangeLockScreen>(), Arg.Any<CancellationToken>())
            .Returns(new PushSettings { PublicKey = PublicKey, Devices = [Phone], ShowAmountsOnLockScreen = true });
        IRenderedComponent<PushSection> cut = RenderSection();
        cut.WaitForState(() => cut.Markup.Contains("Hide amounts on the lock screen", StringComparison.Ordinal));
        AngleSharp.Dom.IElement toggle = cut.Find(".mud-switch input");
        await Assert.That(toggle.HasAttribute("checked")).IsTrue();

        // Act
        await toggle.ChangeAsync(new() { Value = false });

        // Assert
        await _pushService.Received(1).ChangeLockScreenAsync(Arg.Is<ChangeLockScreen>(x => x.ShowAmounts),
            Arg.Any<CancellationToken>());
    }

    private static PushSettings Settings(params PushDevice[] devices) =>
        new() { PublicKey = PublicKey, Devices = devices, ShowAmountsOnLockScreen = false };

    private IRenderedComponent<PushSection> RenderSection()
    {
        Render<MudPopoverProvider>();
        return Render<PushSection>(p => p.AddCascadingValue(new ApplicationState()));
    }

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<PushSection> cut, string text) =>
        cut.FindAll("button").Single(x => x.TextContent.Trim() == text);
}
