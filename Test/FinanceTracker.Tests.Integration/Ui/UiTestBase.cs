using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;
using TUnit.Core;
using Cookie = Microsoft.Playwright.Cookie;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Base for tests that use the running app in a browser, as a user does. Each test gets its own browser context, so
/// its own cookies, and a trace of what happened is kept in the test results when it fails.
/// </summary>
public abstract class UiTestBase : IntegrationTestBase
{
    private IBrowserContext? _context;

    [ClassDataSource<BrowserFixture>(Shared = SharedType.PerTestSession)]
    public required BrowserFixture Browsers { get; init; }

    /// <summary>
    /// Errors the page threw or logged to the console, and requests that failed, which a page working as it should
    /// has none of.
    /// </summary>
    protected List<string> BrowserErrors { get; } = [];

    /// <summary>
    /// A page on <paramref name="screen"/>, signed in as <paramref name="user"/>, or signed out when null. Signs in
    /// with the cookies the user's client signed in with, so tests that aren't about signing in skip the form.
    /// </summary>
    protected async Task<IPage> OpenAsync(ScreenSize screen, SignedInUser? user = null)
    {
        _context = await Browsers.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = App.WebBaseAddress.ToString(),
            // The app is served with the local ASP.NET development certificate, which may not be trusted
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = screen.Width, Height = screen.Height }
        });
        await _context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true });

        if (user is not null)
        {
            await _context.AddCookiesAsync(user.Client.Cookies.GetAllCookies().Select(ToBrowserCookie));
        }

        IPage page = await _context.NewPageAsync();
        page.PageError += (_, error) => BrowserErrors.Add(error);
        page.Console += (_, message) =>
        {
            // A failed request is reported with its address below, the console only says it failed
            if (message.Type == "error" && !message.Text.StartsWith("Failed to load resource", StringComparison.Ordinal))
            {
                BrowserErrors.Add(message.Text);
            }
        };
        page.Response += (_, response) =>
        {
            if (response.Status >= 400)
            {
                BrowserErrors.Add($"{response.Status} from {response.Request.Method} {response.Url}");
            }
        };
        return page;
    }

    /// <summary>
    /// Goes to <paramref name="path"/> and waits until the page runs in the browser, rather than only being
    /// prerendered, so it's ready to use.
    /// </summary>
    protected static async Task GoToAsync(IPage page, string path)
    {
        await page.GotoAsync(path);
        await WaitUntilInteractiveAsync(page);
    }

    /// <summary>
    /// Until the layout says the page runs in the browser, see <c>MainLayout</c>'s <c>data-interactive</c>.
    /// </summary>
    protected static Task WaitUntilInteractiveAsync(IPage page) =>
        page.Locator("[data-interactive]").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Attached
        });

    /// <summary>
    /// Whether the page is wider than the screen, so it scrolls sideways, which no page should on any screen.
    /// </summary>
    protected static Task<bool> ScrollsSidewaysAsync(IPage page) =>
        page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > window.innerWidth");

    [After(Test)]
    public async Task BaseUiCleanupAsync()
    {
        if (_context is null)
        {
            return;
        }

        TestContext? test = TestContext.Current;
        if (test?.Execution.Result?.State is TestState.Failed or TestState.Timeout)
        {
            string traces = Path.Combine(TestContext.ResultsDirectory, "playwright-traces");
            Directory.CreateDirectory(traces);
            string name = string.Concat(test.Metadata.DisplayName.Select(static x =>
                char.IsLetterOrDigit(x) ? x : '-'));
            await _context.Tracing.StopAsync(new TracingStopOptions
            {
                Path = Path.Combine(traces, $"{name}-{Guid.NewGuid():N}.zip")
            });
        }

        await _context.DisposeAsync();
    }

    private static Cookie ToBrowserCookie(System.Net.Cookie cookie) => new()
    {
        Name = cookie.Name,
        Value = cookie.Value,
        Domain = cookie.Domain,
        Path = cookie.Path,
        Secure = cookie.Secure,
        HttpOnly = cookie.HttpOnly
    };
}
