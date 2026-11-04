using FinanceTracker.Tests.Unit.Shared;
using Bunit;
using FinanceTracker.Client;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.Pages;

public class PageComponentTests : ComponentTestBase
{
    private readonly ApplicationState _applicationState = new();

    [Test]
    public async Task Synced_RefreshesThePageInPlace()
    {
        // Arrange
        IRenderedComponent<SyncedTestPage> cut = RenderPage();

        // Act
        await cut.InvokeAsync(_applicationState.NotifySyncedAsync);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Instance.Refreshes).IsEqualTo(1);
            await Assert.That(cut.Markup).Contains("Refreshed 1");
            await Assert.That(_applicationState.Loading).IsFalse();
        }
    }

    [Test]
    public async Task Synced_AfterLeavingThePage_DoesNothing()
    {
        // Arrange
        IRenderedComponent<SyncedTestPage> cut = RenderPage();
        SyncedTestPage page = cut.Instance;
        await DisposeComponentsAsync();

        // Act
        await _applicationState.NotifySyncedAsync();

        // Assert
        await Assert.That(page.Refreshes).IsEqualTo(0);
    }

    [Test]
    public async Task Synced_WhileRefreshing_RefreshesOnceMoreAfterRatherThanAtTheSameTime()
    {
        // Arrange
        IRenderedComponent<SyncedTestPage> cut = RenderPage();
        TaskCompletionSource firstRefresh = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int running = 0;
        int mostAtOnce = 0;
        cut.Instance.Refresh = async () =>
        {
            running++;
            mostAtOnce = Math.Max(mostAtOnce, running);
            if (cut.Instance.Refreshes == 1)
            {
                await firstRefresh.Task;
            }

            running--;
        };

        // Act
        Task first = cut.InvokeAsync(_applicationState.NotifySyncedAsync);
        await cut.InvokeAsync(_applicationState.NotifySyncedAsync);
        await cut.InvokeAsync(_applicationState.NotifySyncedAsync);
        firstRefresh.SetResult();
        await first.WaitAsync(TestTimeouts.Wait);

        // Assert, the two syncs that came in meanwhile make one more refresh
        using (Assert.Multiple())
        {
            await Assert.That(cut.Instance.Refreshes).IsEqualTo(2);
            await Assert.That(mostAtOnce).IsEqualTo(1);
        }
    }

    [Test]
    public async Task Synced_RefreshFails_KeepsThePageAndSaysToReload()
    {
        // Arrange
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<SyncedTestPage> cut = RenderPage();
        cut.Instance.Refresh = static () => throw new HttpRequestException("The API is down");

        // Act
        await cut.InvokeAsync(_applicationState.NotifySyncedAsync);

        // Assert, it tried, and what was shown before stays
        snackbars.WaitForState(() => snackbars.Markup.Contains("reload the page to see the latest"));
        using (Assert.Multiple())
        {
            await Assert.That(cut.Instance.Refreshes).IsEqualTo(1);
            await Assert.That(cut.Markup).Contains("Refreshed 0");
        }
    }

    private IRenderedComponent<SyncedTestPage> RenderPage() =>
        Render<SyncedTestPage>(p => p.AddCascadingValue(_applicationState));
}
