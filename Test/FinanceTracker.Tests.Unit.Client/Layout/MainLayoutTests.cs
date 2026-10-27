using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Client.Layout;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Client.Layout;

public class MainLayoutTests : ComponentTestBase
{
    private static readonly DateTime LastSynced = new(2026, 10, 27, 15, 4, 0);

    private readonly IAutomationService _automationService = Substitute.For<IAutomationService>();
    private readonly ISyncNotifications _syncNotifications = Substitute.For<ISyncNotifications>();
    private readonly ApplicationState _applicationState = new();

    private Func<Task>? _onSyncComplete;

    public MainLayoutTests()
    {
        AddAuthorization();
        AddBunitPersistentComponentState();
        Services.AddSingleton(_automationService);
        Services.AddSingleton(_syncNotifications);

        // Interactive in the browser, as the layout only subscribes once interactive. Builds the services, so last
        SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true));

        _syncNotifications.SubscribeAsync(Arg.Do<Func<Task>>(x => _onSyncComplete = x), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<IDisposable>());
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

    private IRenderedComponent<MainLayout> RenderLayout() =>
        Render<MainLayout>(p => p.AddCascadingValue(_applicationState));
}
