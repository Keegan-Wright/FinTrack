using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Client.Pages;
using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Services;
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
        GivenNotifications(_unread, _read);
        GivenSettings(new NotificationSettings
        {
            Emailed = Enum.GetValues<NotificationKind>(), EmailAddress = "sam@example.test", EmailAvailable = true
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
    public async Task EmailSettings_OneTurnedOff_EmailsTheRest()
    {
        // Arrange
        _service.ChangeSettingsAsync(Arg.Any<ChangeNotificationSettings>(), Arg.Any<CancellationToken>())
            .Returns(call => new NotificationSettings
            {
                Emailed = call.Arg<ChangeNotificationSettings>().Emailed, EmailAddress = "sam@example.test",
                EmailAvailable = true
            });
        IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut = RenderPage();
        cut.WaitForState(() => cut.Markup.Contains("A bank failing to sync", StringComparison.Ordinal));

        // Act
        await Switch(cut, "A bank failing to sync").ChangeAsync(new() { Value = false });

        // Assert
        using (Assert.Multiple())
        {
            await _service.Received(1).ChangeSettingsAsync(
                Arg.Is<ChangeNotificationSettings>(x => !x.Emailed.Contains(NotificationKind.SyncFailed)
                                                        && x.Emailed.Count == 2),
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
            Emailed = Enum.GetValues<NotificationKind>(), EmailAddress = "sam@example.test", EmailAvailable = false
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
            Emailed = Enum.GetValues<NotificationKind>(), EmailAddress = null, EmailAvailable = true
        });

        // Act
        IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut = RenderPage();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("no email address", StringComparison.Ordinal));
        await Assert.That(cut.FindAll("input[type='checkbox']")).IsEmpty();
    }

    private IRenderedComponent<FinanceTracker.Client.Pages.Notifications> RenderPage() =>
        Render<FinanceTracker.Client.Pages.Notifications>(p => p.AddCascadingValue(_applicationState));

    private void GivenNotifications(params Notification[] notifications) =>
        _service.GetNotificationsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => notifications.ToAsyncEnumerable());

    private void GivenSettings(NotificationSettings settings) =>
        _service.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

    private static AngleSharp.Dom.IElement MarkAllButton(IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut) =>
        cut.FindAll("button").Single(static x => x.TextContent.Trim() == "Mark all as read");

    private static AngleSharp.Dom.IElement Switch(IRenderedComponent<FinanceTracker.Client.Pages.Notifications> cut,
        string label) =>
        cut.FindAll(".mud-switch").Single(x => x.TextContent.Contains(label, StringComparison.Ordinal))
            .QuerySelector("input")!;
}
