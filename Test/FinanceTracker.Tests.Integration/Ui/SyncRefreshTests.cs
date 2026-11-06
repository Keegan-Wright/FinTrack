using FinanceTracker.Client.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Integration.OpenBanking;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers.TrueLayer;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// A sync finishing while a page is open, however it was started, updates the page in place, the sync hub tells the
/// browser, and the page loads what the sync brought in without the user reloading. The bank is
/// <see cref="FakeTrueLayer"/>, shared with <see cref="BackgroundSyncTests"/>, so they don't run at once.
/// </summary>
[NotInParallel(nameof(BackgroundSyncTests))]
[ParallelLimiter<UiParallelLimit>]
public class SyncRefreshTests : UiTestBase
{
    private const string Synced = "Synced corner shop";

    /// <summary>
    /// The real clock, whatever day the suite runs on. A sync asked for in a request queues its job there, and Quartz
    /// stamps it with that request's moved clock, so on a day ahead it would wait until then. These are about the
    /// page hearing of the sync, not the day.
    /// </summary>
    protected override DateTimeOffset? DefaultClock => null;

    [Test]
    [Arguments("/Transactions")]
    [Arguments("/Accounts")]
    public async Task Sync_FinishingWhileThePageIsOpen_ShowsWhatItBroughtIn(string path)
    {
        // Arrange, a bank with an account and a payment the app hasn't seen yet
        SignedInUser user = await RegisterUserAsync();
        string accessToken = UniqueName("token");
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", token: accessToken));
        ExternalOpenBankingAccount account = new ExternalOpenBankingAccountFaker().Generate();
        ExternalOpenBankingAccountTransaction payment = new ExternalOpenBankingAccountTransactionFaker().Generate();
        payment.Description = Synced;
        await App.TrueLayer.StubAccountAsync(accessToken, account, transactions: [payment]);

        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await GoToAsync(page, path);
        await WaitUntilLoadedAsync(page);
        string shows = path == "/Accounts" ? account.DisplayName : Synced;
        await Assertions.Expect(page.GetByText(shows)).ToHaveCountAsync(0);

        // Act, a sync asked for somewhere else, e.g. on their phone
        await ApiFor(user).Get<IAutomationService>().RequestSyncAsync(CancellationToken);

        // Assert, here without reloading
        await Assertions.Expect(page.GetByText(shows).First).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions
        {
            Timeout = (float)TestTimeouts.Job.TotalMilliseconds
        });
        await Assert.That(BrowserErrors).IsEmpty();
    }

    [Test]
    public async Task LiveUpdates_LostWhileASyncFinishes_SaysSoThenCatchesUpOnceBack()
    {
        // Arrange, the page live, through a connection the test can drop
        SignedInUser user = await RegisterUserAsync();
        string accessToken = UniqueName("token");
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", token: accessToken));
        ExternalOpenBankingAccount account = new ExternalOpenBankingAccountFaker().Generate();
        ExternalOpenBankingAccountTransaction payment = new ExternalOpenBankingAccountTransactionFaker().Generate();
        payment.Description = Synced;
        await App.TrueLayer.StubAccountAsync(accessToken, account, transactions: [payment]);

        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        IWebSocketRoute? hub = null;
        await page.RouteWebSocketAsync("**/hubs/sync**", socket =>
        {
            socket.ConnectToServer();
            hub = socket;
        });
        await GoToAsync(page, "/Transactions");
        await WaitUntilLoadedAsync(page);
        ILocator paused = page.GetByRole(AriaRole.Status).Filter(new LocatorFilterOptions
        {
            HasText = "Live updates paused"
        });
        // The layout connects once it's rendered in the browser
        using (CancellationTokenSource connecting = new(TestTimeouts.Wait))
        {
            while (Volatile.Read(ref hub) is null)
            {
                await Task.Delay(100, connecting.Token);
            }
        }

        // Act, the connection lost and the hub out of reach, e.g. the server restarting
        await page.RouteAsync("**/hubs/sync/negotiate**", static route => route.AbortAsync());
        await hub!.CloseAsync(new WebSocketRouteCloseOptions { Code = 1011 });

        // Assert
        await Assertions.Expect(paused).ToBeVisibleAsync();
        List<string> problems = await AccessibilityProblemsAsync(page);
        await Assert.That(problems).IsEmpty();

        // Act, a sync finishing while it can't hear, then the hub back
        IAutomationService automation = ApiFor(user).Get<IAutomationService>();
        await automation.RequestSyncAsync(CancellationToken);
        await WaitUntilSyncedAsync(automation);
        await Assertions.Expect(page.GetByText(Synced)).ToHaveCountAsync(0);
        await page.UnrouteAsync("**/hubs/sync/negotiate**");

        // Assert, back within its longest wait between tries, and what it missed is on the page
        LocatorAssertionsToBeVisibleOptions backInTime = new()
        {
            Timeout = (float)(KeepTryingRetryPolicy.Longest + TestTimeouts.Wait).TotalMilliseconds
        };
        await Assertions.Expect(paused).ToBeHiddenAsync(new LocatorAssertionsToBeHiddenOptions
        {
            Timeout = backInTime.Timeout
        });
        await Assertions.Expect(page.GetByText(Synced).First).ToBeVisibleAsync(backInTime);
    }

    /// <summary>
    /// Until the user's accounts have synced, which a new user's never have.
    /// </summary>
    private async Task WaitUntilSyncedAsync(IAutomationService automation)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        timeout.CancelAfter(TestTimeouts.Job);
        while (await automation.GetLastSyncTimeAsync(timeout.Token) is null)
        {
            await Task.Delay(250, timeout.Token);
        }
    }
}
