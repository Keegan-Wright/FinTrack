using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Client.Pages;
using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.Client.Notifications;

public class NotificationsPageTests : ComponentTestBase
{
    private static readonly DateTime RaisedAt = new(2026, 11, 6, 9, 30, 0, DateTimeKind.Utc);

    private readonly INotificationsService _service = Substitute.For<INotificationsService>();
    private readonly ApplicationState _applicationState = new();

    private readonly Notification _unread = new()
    {
        Id = Guid.NewGuid(), Kind = NotificationKind.ConsentEndingSoon, Title = "Bank access ends in 5 days",
        Message = "Renew access before 11 November to keep Bank syncing.", Link = "ProviderSetup",
        RaisedAt = RaisedAt, IsRead = false
    };

    private readonly Notification _read = new()
    {
        Id = Guid.NewGuid(), Kind = NotificationKind.SyncFailed, Title = "Bank couldn't sync",
        Message = "FinTrack couldn't sync Bank.", Link = null, RaisedAt = RaisedAt.AddDays(-1), IsRead = true
    };

    public NotificationsPageTests()
    {
        AddBunitPersistentComponentState();
        Services.AddSingleton(_service);
        Services.AddSingleton(TimeProvider.System);
        Services.AddScoped<IValidator<SetWebhook>, SetWebhookValidator>();
        GivenNotifications(_unread, _read);
        GivenSettings(new NotificationSettings
        {
            Emailed = Enum.GetValues<NotificationKind>(), Webhooked = Enum.GetValues<NotificationKind>(), EmailAddress = "sam@example.test", EmailAvailable = true
        });
    }

    [Test]
    public async Task Render_None_SaysWhatWillShowHere()
    {
        // Arrange
        GivenNotifications();

        // Act
        IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut = RenderPage();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Nothing yet", StringComparison.Ordinal));
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("All read");
            await Assert.That(MarkAllButton(cut).HasAttribute("disabled")).IsTrue();
        }
    }

    [Test]
    public async Task Render_Some_ListsThemNewestFirstSayingWhichAreUnread()
    {
        // Act
        IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut = RenderPage();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains(_unread.Title, StringComparison.Ordinal));
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("1 unread");
            await Assert.That(cut.FindAll(".ft-notification--unread")).Count().IsEqualTo(1);
            await Assert.That(cut.Find(".ft-notification--unread").TextContent).Contains("Unread");
            await Assert.That(cut.Find($"a[href='ProviderSetup']").TextContent.Trim()).IsEqualTo(_unread.Title);
            await Assert.That(cut.FindAll($"[aria-label=\"Mark {_unread.Title} as read\"]")).Count().IsEqualTo(1);
            await Assert.That(cut.FindAll($"[aria-label=\"Mark {_read.Title} as read\"]")).IsEmpty();
        }
    }

    [Test]
    public async Task MarkRead_FromItsButton_ReadsItAndCountsDown()
    {
        // Arrange
        _applicationState.UnreadNotifications = 1;
        _service.MarkReadAsync(_unread.Id, Arg.Any<CancellationToken>()).Returns(true);
        IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut = RenderPage();
        cut.WaitForState(() => cut.Markup.Contains(_unread.Title, StringComparison.Ordinal));

        // Act
        await cut.Find($"[aria-label=\"Mark {_unread.Title} as read\"]").ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-notification--unread").Count == 0);
        using (Assert.Multiple())
        {
            await _service.Received(1).MarkReadAsync(_unread.Id, Arg.Any<CancellationToken>());
            await Assert.That(_applicationState.UnreadNotifications).IsEqualTo(0);
            await Assert.That(cut.Markup).Contains("All read");
        }
    }

    [Test]
    public async Task MarkAllRead_ReadsThemAllAndCountsDown()
    {
        // Arrange
        _applicationState.UnreadNotifications = 1;
        IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut = RenderPage();
        cut.WaitForState(() => cut.Markup.Contains(_unread.Title, StringComparison.Ordinal));

        // Act
        await MarkAllButton(cut).ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-notification--unread").Count == 0);
        using (Assert.Multiple())
        {
            await _service.Received(1).MarkAllReadAsync(Arg.Any<CancellationToken>());
            await Assert.That(_applicationState.UnreadNotifications).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Title_Clicked_MarksItReadOnTheWayWhateverHappensToThePage()
    {
        // Arrange
        _service.MarkReadAsync(_unread.Id, Arg.Any<CancellationToken>()).Returns(true);
        IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut = RenderPage();
        cut.WaitForState(() => cut.Markup.Contains(_unread.Title, StringComparison.Ordinal));

        // Act
        await cut.Find("a[href='ProviderSetup']").ClickAsync(new());

        // Assert, not with the page's token, which leaving the page cancels
        await _service.Received(1).MarkReadAsync(_unread.Id, CancellationToken.None);
    }

    [Test]
    public async Task MarkRead_ServerFails_SaysSoAndLeavesItUnread()
    {
        // Arrange
        _service.MarkReadAsync(_unread.Id, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("The server didn't answer"));
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut = RenderPage();
        cut.WaitForState(() => cut.Markup.Contains(_unread.Title, StringComparison.Ordinal));

        // Act
        await cut.Find($"[aria-label=\"Mark {_unread.Title} as read\"]").ClickAsync(new());

        // Assert
        snackbars.WaitForState(() => snackbars.Markup.Contains("Couldn't mark it as read", StringComparison.Ordinal));
        await Assert.That(cut.FindAll(".ft-notification--unread")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Channels_OneEmailTurnedOff_EmailsTheRestAndKeepsWhatsPosted()
    {
        // Arrange
        _service.ChangeSettingsAsync(Arg.Any<ChangeNotificationSettings>(), Arg.Any<CancellationToken>())
            .Returns(call => new NotificationSettings
            {
                Emailed = call.Arg<ChangeNotificationSettings>().Emailed,
                Webhooked = call.Arg<ChangeNotificationSettings>().Webhooked, EmailAddress = "sam@example.test",
                EmailAvailable = true
            });
        IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut = RenderPage();
        cut.WaitForState(() => BoxCount(cut, "Email:") == 3);

        // Act
        await Box(cut, "Email: A bank failing to sync").ChangeAsync(new() { Value = false });

        // Assert
        using (Assert.Multiple())
        {
            await _service.Received(1).ChangeSettingsAsync(
                Arg.Is<ChangeNotificationSettings>(x => !x.Emailed.Contains(NotificationKind.SyncFailed)
                                                        && x.Emailed.Count == 2 && x.Webhooked.Count == 3),
                Arg.Any<CancellationToken>());
            await Assert.That(cut.Markup).Contains("Sent to sam@example.test");
        }
    }

    [Test]
    public async Task EmailSettings_NoMailServer_SaysTheyOnlyShowHere()
    {
        // Arrange
        GivenSettings(new NotificationSettings
        {
            Emailed = Enum.GetValues<NotificationKind>(), Webhooked = Enum.GetValues<NotificationKind>(), EmailAddress = "sam@example.test", EmailAvailable = false
        });

        // Act
        IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut = RenderPage();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Email isn't set up", StringComparison.Ordinal));
        await Assert.That(cut.FindAll("input[type='checkbox']")).IsEmpty();
    }

    [Test]
    public async Task EmailSettings_NoAddress_SaysTheyOnlyShowHere()
    {
        // Arrange
        GivenSettings(new NotificationSettings
        {
            Emailed = Enum.GetValues<NotificationKind>(), Webhooked = Enum.GetValues<NotificationKind>(), EmailAddress = null, EmailAvailable = true
        });

        // Act
        IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut = RenderPage();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("no email address", StringComparison.Ordinal));
        await Assert.That(cut.FindAll("input[type='checkbox']")).IsEmpty();
    }

    [Test]
    public async Task TestEmail_Sent_SaysSoAndWhenBesideTheButton()
    {
        // Arrange
        _service.SendTestEmailAsync(Arg.Any<CancellationToken>()).Returns(new NotificationSettings
        {
            Emailed = Enum.GetValues<NotificationKind>(), Webhooked = Enum.GetValues<NotificationKind>(), EmailAddress = "sam@example.test", EmailAvailable = true,
            LastEmail = new DeliveryStatus { At = RaisedAt }
        });
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut = RenderPage();
        cut.WaitForState(() => cut.Markup.Contains("Send a test email", StringComparison.Ordinal));

        // Act
        await TestButton(cut).ClickAsync(new());

        // Assert
        snackbars.WaitForState(() =>
            snackbars.Markup.Contains("Test email sent to sam@example.test", StringComparison.Ordinal));
        using (Assert.Multiple())
        {
            await _service.Received(1).SendTestEmailAsync(Arg.Any<CancellationToken>());
            await Assert.That(cut.Find(".ft-notification-test").TextContent).Contains("Last email sent");
        }
    }

    [Test]
    public async Task TestEmail_Fails_SaysWhyBesideTheButton()
    {
        // Arrange
        _service.SendTestEmailAsync(Arg.Any<CancellationToken>()).Returns(new NotificationSettings
        {
            Emailed = Enum.GetValues<NotificationKind>(), Webhooked = Enum.GetValues<NotificationKind>(), EmailAddress = "sam@example.test", EmailAvailable = true,
            LastEmail = new DeliveryStatus
            {
                At = RaisedAt, Failure = "The mail server didn't accept FinTrack's sign in."
            }
        });
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut = RenderPage();
        cut.WaitForState(() => cut.Markup.Contains("Send a test email", StringComparison.Ordinal));

        // Act
        await TestButton(cut).ClickAsync(new());

        // Assert
        snackbars.WaitForState(() =>
            snackbars.Markup.Contains("Couldn't send the test email", StringComparison.Ordinal));
        AngleSharp.Dom.IElement status = cut.Find(".ft-notification-test .mud-error-text");
        await Assert.That(status.TextContent).Contains("The mail server didn't accept FinTrack's sign in.");
    }

    [Test]
    public async Task TestEmail_EmailNoLongerPossible_ReloadsTheSettingsToSayWhy()
    {
        // Arrange
        _service.SendTestEmailAsync(Arg.Any<CancellationToken>()).Returns((NotificationSettings?)null);
        IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut = RenderPage();
        cut.WaitForState(() => cut.Markup.Contains("Send a test email", StringComparison.Ordinal));
        GivenSettings(new NotificationSettings
        {
            Emailed = Enum.GetValues<NotificationKind>(), Webhooked = Enum.GetValues<NotificationKind>(), EmailAddress = "sam@example.test", EmailAvailable = false
        });

        // Act
        await TestButton(cut).ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Email isn't set up", StringComparison.Ordinal));
    }

    [Test]
    public async Task Channels_EmailOnly_HasAnEmailColumnAndNoWebhookOne()
    {
        // Act
        IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut = RenderPage();

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-channels").Count == 1);
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindAll(".ft-channels thead th").Select(static x => x.TextContent.Trim()))
                .IsEquivalentTo(["Notification", "Email"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(BoxCount(cut, "Webhook:")).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Channels_WebhookSet_PostedOneTurnedOffKeepsWhatsEmailed()
    {
        // Arrange
        GivenSettings(new NotificationSettings
        {
            Emailed = Enum.GetValues<NotificationKind>(), Webhooked = Enum.GetValues<NotificationKind>(),
            EmailAddress = "sam@example.test", EmailAvailable = true,
            WebhookUrl = "https://hooks.example.test/fintrack", WebhookSecret = "secret"
        });
        _service.ChangeSettingsAsync(Arg.Any<ChangeNotificationSettings>(), Arg.Any<CancellationToken>())
            .Returns(call => new NotificationSettings
            {
                Emailed = call.Arg<ChangeNotificationSettings>().Emailed,
                Webhooked = call.Arg<ChangeNotificationSettings>().Webhooked, EmailAddress = "sam@example.test",
                EmailAvailable = true, WebhookUrl = "https://hooks.example.test/fintrack", WebhookSecret = "secret"
            });
        IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut = RenderPage();
        cut.WaitForState(() => BoxCount(cut, "Webhook:") == 3);

        // Act
        await Box(cut, "Webhook: A bank's access having ended").ChangeAsync(new() { Value = false });

        // Assert
        await _service.Received(1).ChangeSettingsAsync(
            Arg.Is<ChangeNotificationSettings>(x => !x.Webhooked.Contains(NotificationKind.ConsentEnded)
                                                    && x.Webhooked.Count == 2 && x.Emailed.Count == 3),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Channels_NeitherEmailNorWebhook_SaysWhereToSetOneUp()
    {
        // Arrange
        GivenSettings(new NotificationSettings
        {
            Emailed = Enum.GetValues<NotificationKind>(), Webhooked = Enum.GetValues<NotificationKind>(),
            EmailAddress = null, EmailAvailable = false
        });

        // Act
        IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut = RenderPage();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Nowhere else yet", StringComparison.Ordinal));
        await Assert.That(cut.FindAll(".ft-channels")).IsEmpty();
    }

    private static AngleSharp.Dom.IElement TestButton(
        IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut) =>
        cut.FindAll("button").Single(static x => x.TextContent.Trim() == "Send a test email");

    private IRenderedComponent<FinanceTracker.Client.Pages.Notifications> RenderPage() =>
        Render<FinanceTracker.Client.Pages.Notifications>(p => p.AddCascadingValue(_applicationState));

    private void GivenNotifications(params Notification[] notifications) =>
        _service.GetNotificationsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => notifications.ToAsyncEnumerable());

    private void GivenSettings(NotificationSettings settings) =>
        _service.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

    private static AngleSharp.Dom.IElement MarkAllButton(IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut) =>
        cut.FindAll("button").Single(static x => x.TextContent.Trim() == "Mark all as read");

    /// <summary>
    /// The checkbox named <paramref name="label"/>, found by its name as a screen reader reads it: MudBlazor labels
    /// it by a hidden span.
    /// </summary>
    private static AngleSharp.Dom.IElement Box(IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut,
        string label)
    {
        string id = cut.FindAll(".mud-sr-only").Single(x => x.TextContent == label).Id!;
        return cut.Find($"input[aria-labelledby='{id}']");
    }

    private static int BoxCount(IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut, string prefix) =>
        cut.FindAll(".mud-sr-only").Count(x => x.TextContent.StartsWith(prefix, StringComparison.Ordinal));
}
