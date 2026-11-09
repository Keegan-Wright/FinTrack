using Bunit;
using Bunit.TestDoubles;
using FinanceTracker.Client;
using FinanceTracker.Client.Layout;
using FinanceTracker.Contracts.OpenBanking;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.Client.Layout;

public class MainLayoutTests : ComponentTestBase
{
    private static readonly DateTime LastSynced = new(2026, 10, 27, 15, 4, 0);

    private readonly IAutomationService _automationService = Substitute.For<IAutomationService>();
    private readonly ISyncNotifications _syncNotifications = Substitute.For<ISyncNotifications>();
    private readonly ITransfersService _transfersService = Substitute.For<ITransfersService>();
    private readonly IBillsService _billsService = Substitute.For<IBillsService>();
    private readonly IOpenBankingService _openBankingService = Substitute.For<IOpenBankingService>();
    private readonly INotificationsService _notificationsService = Substitute.For<INotificationsService>();
    private readonly IBrowserViewportService _viewport = Substitute.For<IBrowserViewportService>();
    private readonly ApplicationState _applicationState = new();
    private readonly AppUpdate _update = new("0.1.1+3e871e2");

    private Func<Task>? _onSyncComplete;
    private Func<Task>? _onSyncStarted;
    private Func<Task>? _onNotificationAdded;

    public MainLayoutTests()
    {
        AddAuthorization();
        AddBunitPersistentComponentState();
        Services.AddSingleton(_automationService);
        Services.AddSingleton(_syncNotifications);
        Services.AddSingleton(_transfersService);
        Services.AddSingleton(_billsService);
        Services.AddSingleton(_openBankingService);
        Services.AddSingleton(_notificationsService);
        Services.AddSingleton(_viewport);
        Services.AddSingleton(_update);

        // Interactive in the browser, as the layout only subscribes once interactive. Builds the services, so last
        SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true));

        _syncNotifications.SubscribeAsync(Arg.Do<Func<Task>>(x => _onSyncComplete = x), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<IDisposable>());
        _syncNotifications.SubscribeToSyncStartsAsync(Arg.Do<Func<Task>>(x => _onSyncStarted = x),
                Arg.Any<CancellationToken>())
            .Returns(Substitute.For<IDisposable>());
        _syncNotifications.SubscribeToNotificationsAsync(Arg.Do<Func<Task>>(x => _onNotificationAdded = x),
                Arg.Any<CancellationToken>())
            .Returns(Substitute.For<IDisposable>());
        GivenConnections();
    }

    [Test]
    public async Task Render_NeverSynced_ShowsNoSyncStatus()
    {
        // Arrange
        _automationService.GetLastSyncTimeAsync(Arg.Any<CancellationToken>()).Returns((DateTime?)null);

        // Act
        IRenderedComponent<MainLayout> cut = RenderLayout();

        // Assert
        await Assert.That(cut.FindAll(".ft-sync-status")).IsEmpty();
    }

    [Test]
    public async Task Render_Synced_ShowsWhenWithoutSayingItFinished()
    {
        // Arrange
        using CultureScope culture = new();
        _automationService.GetLastSyncTimeAsync(Arg.Any<CancellationToken>()).Returns(LastSynced);

        // Act
        IRenderedComponent<MainLayout> cut = RenderLayout();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Find(".ft-sync-status").TextContent).Contains("27 Oct, 15:04");
            await Assert.That(cut.Markup).DoesNotContain("Your accounts have been synced");
        }
    }

    [Test]
    public async Task Render_FirstSyncQueued_ShowsItSyncing()
    {
        // Arrange
        _automationService.GetLastSyncTimeAsync(Arg.Any<CancellationToken>()).Returns((DateTime?)null);
        IRenderedComponent<MainLayout> cut = RenderLayout();

        // Act, as the connect page does once a bank is connected
        await cut.InvokeAsync(() => _applicationState.Syncing = true);

        // Assert
        await Assert.That(cut.Find(".ft-sync-status").TextContent).Contains("Syncing your accounts…");
    }

    [Test]
    public async Task SyncNow_QueuesASyncAndShowsItSyncing()
    {
        // Arrange
        _automationService.GetLastSyncTimeAsync(Arg.Any<CancellationToken>()).Returns(LastSynced);
        IRenderedComponent<MainLayout> cut = RenderLayout();

        // Act
        await cut.Find("[aria-label='Sync now']").ClickAsync(new());

        // Assert
        using (Assert.Multiple())
        {
            await _automationService.Received(1).RequestSyncAsync(Arg.Any<CancellationToken>());
            await Assert.That(_applicationState.Syncing).IsTrue();
            await Assert.That(cut.Find(".ft-sync-status").TextContent).Contains("Syncing your accounts…");
        }
    }

    [Test]
    public async Task SyncNow_WhenItCantBeQueued_StopsShowingItSyncingAndSaysSo()
    {
        // Arrange
        _automationService.GetLastSyncTimeAsync(Arg.Any<CancellationToken>()).Returns(LastSynced);
        _automationService.RequestSyncAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("The server didn't answer"));
        IRenderedComponent<MainLayout> cut = RenderLayout();

        // Act
        await cut.Find("[aria-label='Sync now']").ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Couldn't start a sync, try again"));
        using (Assert.Multiple())
        {
            await Assert.That(_applicationState.Syncing).IsFalse();
            await Assert.That(cut.Find(".ft-sync-status").TextContent).Contains("Last synced");
            await Assert.That(cut.FindAll("[aria-label='Sync now']")).Count().IsEqualTo(1);
        }
    }

    [Test]
    public async Task SyncFinished_SaysSoAndShowsTheNewTime()
    {
        // Arrange
        using CultureScope culture = new();
        _automationService.GetLastSyncTimeAsync(Arg.Any<CancellationToken>()).Returns((DateTime?)null, LastSynced);
        IRenderedComponent<MainLayout> cut = RenderLayout();
        await cut.InvokeAsync(() => _applicationState.Syncing = true);

        // Act
        await cut.InvokeAsync(_onSyncComplete!);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(_applicationState.Syncing).IsFalse();
            await Assert.That(cut.Find(".ft-sync-status").TextContent).Contains("27 Oct, 15:04");
            await Assert.That(cut.Markup).Contains("Your accounts have been synced");
        }
    }

    [Test]
    public async Task Render_TransfersToReview_ShowsTheCountOnTheReviewLink()
    {
        // Arrange
        _transfersService.GetReviewCountAsync(Arg.Any<CancellationToken>()).Returns(3);

        // Act
        IRenderedComponent<MainLayout> cut = RenderLayout();

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-nav__count").Count > 0);
        await Assert.That(cut.Find(".ft-nav__count").TextContent.Trim()).IsEqualTo("3");
    }

    [Test]
    public async Task Render_SubscriptionsToReview_ShowsTheCountOnTheirLink()
    {
        // Arrange
        _billsService.GetSuggestionCountAsync(Arg.Any<CancellationToken>()).Returns(4);

        // Act
        IRenderedComponent<MainLayout> cut = RenderLayout();

        // Assert
        cut.WaitForState(() => cut.FindAll("a[href='Review/Subscriptions'] .ft-nav__count").Count > 0);
        await Assert.That(cut.Find("a[href='Review/Subscriptions'] .ft-nav__count").TextContent.Trim())
            .IsEqualTo("4");
    }

    [Test]
    public async Task Render_NothingToReview_ShowsNoCount()
    {
        // Arrange
        _transfersService.GetReviewCountAsync(Arg.Any<CancellationToken>()).Returns(0);

        // Act
        IRenderedComponent<MainLayout> cut = RenderLayout();

        // Assert
        await Assert.That(cut.FindAll(".ft-nav__count")).IsEmpty();
    }

    [Test]
    public async Task SyncCompleted_RefreshesTheReviewCount()
    {
        // Arrange
        _transfersService.GetReviewCountAsync(Arg.Any<CancellationToken>()).Returns(0, 2);
        IRenderedComponent<MainLayout> cut = RenderLayout();

        // Act
        await cut.InvokeAsync(_onSyncComplete!);

        // Assert
        await Assert.That(cut.Find(".ft-nav__count").TextContent.Trim()).IsEqualTo("2");
    }

    [Test]
    public async Task SyncStarted_OnItsScheduleOrElsewhere_ShowsItSyncingUntilItFinishes()
    {
        // Arrange
        IRenderedComponent<MainLayout> cut = RenderLayout();
        cut.WaitForState(() => _onSyncStarted is not null && _onSyncComplete is not null);

        // Act
        await cut.InvokeAsync(_onSyncStarted!);
        bool syncing = cut.Markup.Contains("Syncing your accounts");
        await cut.InvokeAsync(_onSyncComplete!);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(syncing).IsTrue();
            await Assert.That(cut.Markup).DoesNotContain("Syncing your accounts");
        }
    }

    [Test]
    public async Task SyncCompleted_TellsThePageToRefresh()
    {
        // Arrange
        int refreshes = 0;
        _applicationState.Synced += () =>
        {
            refreshes++;
            return Task.CompletedTask;
        };
        IRenderedComponent<MainLayout> cut = RenderLayout();

        // Act
        await cut.InvokeAsync(_onSyncComplete!);

        // Assert
        await Assert.That(refreshes).IsEqualTo(1);
    }

    [Test]
    public async Task Render_ConnectionsToRenew_ShowsTheBannerAndTheCountOnTheConnectionsLink()
    {
        // Arrange
        GivenConnections(ConsentState.Active, ConsentState.RenewSoon, ConsentState.Lapsed);

        // Act
        IRenderedComponent<MainLayout> cut = RenderLayout();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Find(".ft-renew-banner").TextContent)
                .Contains("2 bank connections need renewing to keep syncing.");
            await Assert.That(cut.Find(".ft-renew-banner a").GetAttribute("href")).IsEqualTo("ProviderSetup");
            await Assert.That(cut.Find("a[href='ProviderSetup'] .ft-nav__count").TextContent.Trim()).IsEqualTo("2");
        }
    }

    [Test]
    public async Task Render_AllConnectionsActive_ShowsNoBannerOrCount()
    {
        // Arrange
        GivenConnections(ConsentState.Active);

        // Act
        IRenderedComponent<MainLayout> cut = RenderLayout();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindAll(".ft-renew-banner")).IsEmpty();
            await Assert.That(cut.FindAll("a[href='ProviderSetup'] .ft-nav__count")).IsEmpty();
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Render_OpensTheDrawerOnlyAtDesktopWidth(bool desktop)
    {
        // Arrange
        _viewport.IsBreakpointWithinWindowSizeAsync(Breakpoint.LgAndUp).Returns(desktop);

        // Act
        IRenderedComponent<MainLayout> cut = RenderLayout();

        // Assert, and the static input package's script, which would restore a remembered state, leaves it alone
        using (Assert.Multiple())
        {
            await Assert.That(cut.Find(".mud-drawer").ClassList.Contains("mud-drawer--open")).IsEqualTo(desktop);
            await Assert.That(cut.Find(".mud-drawer").GetAttribute("data-mud-static-monitored")).IsEqualTo("true");
        }
    }

    [Test]
    public async Task MenuButton_TogglesTheDrawer()
    {
        // Arrange
        _viewport.IsBreakpointWithinWindowSizeAsync(Breakpoint.LgAndUp).Returns(false);
        IRenderedComponent<MainLayout> cut = RenderLayout();

        // Act
        cut.Find("button[aria-label='Toggle navigation']").Click();

        // Assert
        await Assert.That(cut.Find(".mud-drawer").ClassList.Contains("mud-drawer--open")).IsTrue();
    }

    private void GivenConnections(params ConsentState[] states) =>
        _openBankingService.GetConnectionsAsync(Arg.Any<CancellationToken>()).Returns(_ => states
            .Select(static (state, index) => new BankConnectionSummary
            {
                ConnectionId = Guid.NewGuid(),
                Name = $"Bank {index}",
                Logo = [],
                ConsentExpiresAt = DateTime.UtcNow.AddDays(30),
                ConsentState = state
            })
            .ToAsyncEnumerable());

    [Test]
    public async Task Render_ServerFailsAsItStarts_StillShowsTheMenuAndPage()
    {
        // Arrange, the last sync and the counts all fail
        _automationService.GetLastSyncTimeAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("The server didn't answer"));
        _transfersService.GetReviewCountAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("The server didn't answer"));

        // Act
        IRenderedComponent<MainLayout> cut = RenderLayout();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindAll("#nav-drawer")).Count().IsEqualTo(1);
            await Assert.That(cut.FindAll(".ft-sync-status")).IsEmpty();
        }
    }

    [Test]
    public async Task Render_SyncHubUnreachable_StillShowsTheMenuAndWhenItLastSynced()
    {
        // Arrange
        _syncNotifications.SubscribeAsync(Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("The hub didn't answer"));
        _automationService.GetLastSyncTimeAsync(Arg.Any<CancellationToken>()).Returns(LastSynced);

        // Act
        IRenderedComponent<MainLayout> cut = RenderLayout();

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-sync-status").Count == 1);
        await Assert.That(cut.Find(".ft-sync-status").TextContent).Contains("Last synced");
    }

    [Test]
    public async Task Render_NoUpdate_ShowsNoUpdateBanner()
    {
        // Act
        IRenderedComponent<MainLayout> cut = RenderLayout();

        // Assert
        cut.WaitForState(() => _onSyncStarted is not null);
        await Assert.That(cut.FindAll(".ft-update-banner")).IsEmpty();
    }

    [Test]
    public async Task Update_HeardWhileOpen_SaysSoAndReloadsWhenAsked()
    {
        // Arrange
        IRenderedComponent<MainLayout> cut = RenderLayout();
        cut.WaitForState(() => _onSyncStarted is not null);

        // Act
        await cut.InvokeAsync(() => _update.ServerRuns("0.1.2+9a8b7c6"));
        cut.WaitForState(() => cut.FindAll(".ft-update-banner").Count == 1);
        string said = cut.Find(".ft-update-banner").TextContent;
        await cut.InvokeAsync(() => cut.Find(".ft-update-banner__reload").Click());

        // Assert, the whole app loaded again, only when asked
        BunitNavigationManager navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        using (Assert.Multiple())
        {
            await Assert.That(said).Contains("FinTrack has been updated");
            await Assert.That(navigation.History.First().Options.ForceLoad).IsTrue();
        }
    }

    [Test]
    public async Task Page_FailsAfterAnUpdate_AsksToReloadRatherThanTryAgain()
    {
        // Arrange
        _update.ServerRuns("0.1.2+9a8b7c6");
        RenderFragment page = builder =>
        {
            builder.OpenComponent<FailingPage>(0);
            builder.AddAttribute(1, nameof(FailingPage.Fail), (Func<bool>)(() => true));
            builder.CloseComponent();
        };

        // Act
        IRenderedComponent<MainLayout> cut = Render<MainLayout>(p => p
            .AddCascadingValue(_applicationState)
            .Add(x => x.Body, page));

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-page-header__title").Count == 1);
        IEnumerable<string> buttons = cut.FindAll(".ft-page button").Select(static x => x.TextContent.Trim());
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("This page needs the latest FinTrack");
            await Assert.That(buttons).Contains("Reload");
            await Assert.That(buttons).DoesNotContain("Try again");
        }
    }

    [Test]
    public async Task Render_UnreadNotifications_TheBellSaysHowMany()
    {
        // Arrange
        _notificationsService.GetUnreadCountAsync(Arg.Any<CancellationToken>()).Returns(3);

        // Act
        IRenderedComponent<MainLayout> cut = RenderLayout();

        // Assert
        cut.WaitForState(() => cut.FindAll("[aria-label='Notifications, 3 unread']").Count == 1);
        await Assert.That(cut.Find("[aria-label='Notifications, 3 unread']").GetAttribute("href"))
            .IsEqualTo("Notifications");
    }

    [Test]
    public async Task NotificationAdded_WhileOpen_TheBellCountsItWithoutAReload()
    {
        // Arrange
        _notificationsService.GetUnreadCountAsync(Arg.Any<CancellationToken>()).Returns(0, 1);
        IRenderedComponent<MainLayout> cut = RenderLayout();
        cut.WaitForState(() => _onNotificationAdded is not null);
        cut.WaitForState(() => cut.FindAll("a[aria-label='Notifications']").Count == 1);

        // Act
        await cut.InvokeAsync(_onNotificationAdded!);

        // Assert
        cut.WaitForState(() => cut.FindAll("a[aria-label='Notifications, 1 unread']").Count == 1);
        await Assert.That(_applicationState.UnreadNotifications).IsEqualTo(1);
    }

    [Test]
    public async Task LiveUpdates_Paused_SaysSoInTheAppBar()
    {
        // Arrange
        IRenderedComponent<MainLayout> cut = RenderLayout();
        cut.WaitForState(() => _onSyncStarted is not null);

        // Act
        LiveUpdatesPause(paused: true);

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-live-status__paused").Count == 1);
        await Assert.That(cut.Find("[role=status].ft-live-status").TextContent).Contains("Live updates paused");
    }

    [Test]
    public async Task LiveUpdates_Resumed_StopsSayingSoAndCatchesUpOnWhatItMissed()
    {
        // Arrange
        _automationService.GetLastSyncTimeAsync(Arg.Any<CancellationToken>()).Returns(LastSynced);
        bool pageRefreshed = false;
        _applicationState.Synced += () =>
        {
            pageRefreshed = true;
            return Task.CompletedTask;
        };
        IRenderedComponent<MainLayout> cut = RenderLayout();
        cut.WaitForState(() => _onSyncStarted is not null);
        LiveUpdatesPause(paused: true);
        cut.WaitForState(() => cut.FindAll(".ft-live-status__paused").Count == 1);
        _automationService.ClearReceivedCalls();

        // Act
        LiveUpdatesPause(paused: false);

        // Assert, quietly, nothing says what it missed
        cut.WaitForState(() => pageRefreshed);
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindAll(".ft-live-status__paused")).IsEmpty();
            await Assert.That(_automationService.ReceivedCalls()
                .Count(x => x.GetMethodInfo().Name == nameof(IAutomationService.GetLastSyncTimeAsync))).IsEqualTo(1);
            await Assert.That(cut.FindAll(".mud-snackbar")).IsEmpty();
        }
    }

    [Test]
    [Arguments(true, false)]
    [Arguments(false, true)]
    public async Task LiveUpdates_Resumed_StopsShowingASyncOnlyIfOneFinishedMeanwhile(bool finishedMeanwhile,
        bool stillSyncing)
    {
        // Arrange, a sync started, then live updates paused before it finished
        _automationService.GetLastSyncTimeAsync(Arg.Any<CancellationToken>()).Returns(LastSynced);
        bool caughtUp = false;
        _applicationState.Synced += () =>
        {
            caughtUp = true;
            return Task.CompletedTask;
        };
        IRenderedComponent<MainLayout> cut = RenderLayout();
        cut.WaitForState(() => _onSyncStarted is not null);
        await cut.InvokeAsync(_onSyncStarted!);
        LiveUpdatesPause(paused: true);
        if (finishedMeanwhile)
        {
            _automationService.GetLastSyncTimeAsync(Arg.Any<CancellationToken>()).Returns(LastSynced.AddMinutes(2));
        }

        // Act
        LiveUpdatesPause(paused: false);

        // Assert
        cut.WaitForState(() => caughtUp);
        await Assert.That(_applicationState.Syncing).IsEqualTo(stillSyncing);
    }

    [Test]
    public async Task Page_FailsToLoad_SaysSoInItsPlaceAndLoadsWhenTriedAgain()
    {
        // Arrange
        bool serverDown = true;
        RenderFragment page = builder =>
        {
            builder.OpenComponent<FailingPage>(0);
            builder.AddAttribute(1, nameof(FailingPage.Fail), (Func<bool>)(() => serverDown));
            builder.CloseComponent();
        };

        // Act
        IRenderedComponent<MainLayout> cut = Render<MainLayout>(p => p
            .AddCascadingValue(_applicationState)
            .Add(x => x.Body, page));

        // Assert, in the page's place, the layout around it still there
        cut.WaitForState(() => cut.Markup.Contains("Something went wrong"));
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).DoesNotContain(FailingPage.Loaded);
            await Assert.That(cut.FindAll("#nav-drawer")).Count().IsEqualTo(1);
        }

        // Act, the server's back
        serverDown = false;
        await cut.FindAll("button").Single(static x => x.TextContent.Trim() == "Try again").ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.Markup.Contains(FailingPage.Loaded));
        await Assert.That(cut.Markup).DoesNotContain("Something went wrong");
    }

    [Test]
    public async Task Page_LeftWhileStillLoading_DoesNotFailTheNextPage()
    {
        // Arrange, a page loading when the user goes to another
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RenderFragment slowPage = builder =>
        {
            builder.OpenComponent<SlowPage>(0);
            builder.AddAttribute(1, nameof(SlowPage.Gate), gate);
            builder.CloseComponent();
        };
        RenderFragment nextPage = builder =>
        {
            builder.OpenComponent<FailingPage>(0);
            builder.AddAttribute(1, nameof(FailingPage.Fail), (Func<bool>)(static () => false));
            builder.CloseComponent();
        };
        IRenderedComponent<MainLayout> cut = Render<MainLayout>(p => p
            .AddCascadingValue(_applicationState)
            .Add(x => x.Body, slowPage));
        cut.WaitForState(() => cut.Markup.Contains("Still loading"));

        // Act, on to the next page, then the first page's load carries on
        cut.Render(p => p.Add(x => x.Body, nextPage));
        cut.WaitForState(() => cut.Markup.Contains(FailingPage.Loaded));
        gate.SetResult();
        await Task.Delay(100);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains(FailingPage.Loaded);
            await Assert.That(cut.Markup).DoesNotContain("Something went wrong");
        }
    }

    /// <summary>
    /// The sync hub's connection lost, or back, as the browser's sync notifications say.
    /// </summary>
    private void LiveUpdatesPause(bool paused)
    {
        _syncNotifications.Paused.Returns(paused);
        _syncNotifications.PausedChanged += Raise.Event();
    }

    private IRenderedComponent<MainLayout> RenderLayout() =>
        Render<MainLayout>(p => p.AddCascadingValue(_applicationState));
}
