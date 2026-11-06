using System.Globalization;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
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
    /// The day to run every test on, from <c>FINTRACK_TESTS_NOW</c>, e.g. <c>2026-03-01T12:00:00Z</c>, to see how
    /// the whole suite does on the 1st of a month. Now when it isn't set, as CI runs.
    /// </summary>
    protected override DateTimeOffset? DefaultClock { get; } = ClockFromEnvironment();

    private static DateTimeOffset? ClockFromEnvironment()
    {
        string? value = Environment.GetEnvironmentVariable("FINTRACK_TESTS_NOW");
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A page on <paramref name="screen"/>, signed in as <paramref name="user"/>, or signed out when null. Signs in
    /// with the cookies the user's client signed in with, so tests that aren't about signing in skip the form. The
    /// app follows the system's light or dark setting, <paramref name="dark"/> here.
    /// </summary>
    protected async Task<IPage> OpenAsync(ScreenSize screen, SignedInUser? user = null, bool dark = false)
    {
        _context = await Browsers.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = App.WebBaseAddress.ToString(),
            // The app is served with the local ASP.NET development certificate, which may not be trusted
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = screen.Width, Height = screen.Height },
            ColorScheme = dark ? ColorScheme.Dark : ColorScheme.Light
        });
        await _context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true });

        if (user is not null)
        {
            // The test clock's cookie among them, for the server
            await _context.AddCookiesAsync(user.Client.Cookies.GetAllCookies().Select(ToBrowserCookie));
        }

        // And the browser's date to match, the pages run in it. Only the date, installing a whole fake clock fakes
        // the timers too, which MudBlazor's selects trip over
        if (user is { ClockOffset: var offset } && offset != TimeSpan.Zero)
        {
            await _context.Clock.SetFixedTimeAsync(DateTime.UtcNow + offset);
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
    /// A new user with something of everything, see <see cref="SeededUser"/>. Given <paramref name="at"/>, it's
    /// what they have on that day, and the app and browser read that as now for them.
    /// </summary>
    protected async Task<SignedInUser> RegisterSeededUserAsync(DateTimeOffset? at = null)
    {
        SignedInUser user = await RegisterUserAsync(at: at);
        await SeededUser.SeedAsync(Scenario, user.Username, NowFor(user));
        return user;
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
    /// Until nothing on the page is loading, neither a section's spinner (<c>LoadingFrame</c>) nor the layout's
    /// overlay, so what's checked is the page as the user ends up seeing it. An open dialog's backdrop is an
    /// overlay too, which isn't loading.
    /// </summary>
    protected static Task WaitUntilLoadedAsync(IPage page) =>
        page.WaitForFunctionAsync(
            "() => !document.querySelector('.ft-loading-frame__overlay, .mud-overlay:not(.mud-overlay-dialog)')");

    /// <summary>
    /// The dialog that's open, as MudBlazor shows one.
    /// </summary>
    protected static ILocator Dialog(IPage page) => page.Locator(".mud-dialog");

    /// <summary>
    /// The button named <paramref name="name"/>, exactly.
    /// </summary>
    protected static ILocator Button(IPage page, string name) =>
        page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = name, Exact = true });

    /// <summary>
    /// Opens the ⋮ menu labelled <c>Actions for {name}</c> and clicks <paramref name="item"/> in it.
    /// </summary>
    protected static async Task ChooseFromRowMenuAsync(IPage page, string name, string item)
    {
        await Button(page, $"Actions for {name}").ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions { Name = item, Exact = true }).ClickAsync();
    }

    /// <summary>
    /// Opens the select labelled <paramref name="label"/> in <paramref name="within"/> and takes its first option.
    /// </summary>
    protected static async Task ChooseFirstOptionAsync(IPage page, ILocator within, string label)
    {
        await within.GetByRole(AriaRole.Combobox, new LocatorGetByRoleOptions { Name = label }).ClickAsync();
        await page.Locator(".mud-popover-open .mud-list-item").First.ClickAsync();
    }

    /// <summary>
    /// Opens the select labelled <paramref name="label"/> in <paramref name="within"/> and takes the option that
    /// reads <paramref name="option"/>.
    /// </summary>
    protected static async Task ChooseOptionAsync(IPage page, ILocator within, string label, string option)
    {
        await within.GetByRole(AriaRole.Combobox, new LocatorGetByRoleOptions { Name = label }).ClickAsync();
        ILocator item = page.Locator(".mud-popover-open .mud-list-item")
            .Filter(new LocatorFilterOptions { HasText = option }).First;
        await item.ClickAsync();
    }

    /// <summary>
    /// Ticks <paramref name="option"/> in the multiple choice select labelled <paramref name="label"/>, leaving it
    /// open. Under a full parallel run a click as the list opens is sometimes missed, so like someone who sees it
    /// didn't tick, it's clicked again.
    /// </summary>
    protected static async Task TickOptionAsync(IPage page, ILocator within, string label, string option)
    {
        await within.GetByRole(AriaRole.Combobox, new LocatorGetByRoleOptions { Name = label }).ClickAsync();
        ILocator item = page.Locator(".mud-popover-open .mud-list-item")
            .Filter(new LocatorFilterOptions { HasText = option }).First;
        ILocator ticked = page.Locator(".mud-popover-open .mud-selected-item")
            .Filter(new LocatorFilterOptions { HasText = option });

        await item.ClickAsync();
        try
        {
            await Assertions.Expect(ticked).ToHaveCountAsync(1, new LocatorAssertionsToHaveCountOptions
            {
                Timeout = 3_000
            });
        }
        catch (PlaywrightException)
        {
            await item.ClickAsync();
            await Assertions.Expect(ticked).ToHaveCountAsync(1);
        }
    }

    /// <summary>
    /// Opens the date picker labelled <paramref name="label"/> in <paramref name="within"/>, which can't be typed
    /// into, picks today, and closes it by clicking the dialog's title as someone would click away.
    /// </summary>
    protected static async Task PickTodayAsync(IPage page, ILocator within, string label)
    {
        await within.GetByLabel(label, new LocatorGetByLabelOptions { Exact = true }).ClickAsync();
        await page.Locator(".mud-popover-open .mud-picker-calendar-day.mud-current").First.ClickAsync();
        await within.Locator(".mud-dialog-title").ClickAsync();
    }

    /// <summary>
    /// The file <paramref name="start"/> has the browser download, once it's done.
    /// </summary>
    protected static async Task<IDownload> DownloadAsync(IPage page, Func<Task> start)
    {
        IDownload download = await page.RunAndWaitForDownloadAsync(start);
        await download.PathAsync();
        return download;
    }

    /// <summary>
    /// What <paramref name="download"/> holds, as text.
    /// </summary>
    protected static async Task<string> TextOfAsync(IDownload download) =>
        await File.ReadAllTextAsync(await download.PathAsync());

    /// <summary>
    /// The first <paramref name="count"/> bytes of <paramref name="download"/> as ASCII, e.g. <c>%PDF-</c>.
    /// </summary>
    protected static async Task<string> StartOfAsync(IDownload download, int count)
    {
        byte[] bytes = await File.ReadAllBytesAsync(await download.PathAsync());
        return System.Text.Encoding.ASCII.GetString(bytes, 0, Math.Min(count, bytes.Length));
    }

    /// <summary>
    /// What makes the page wider than the screen, so it scrolls sideways, which no page should on any screen: the
    /// outermost elements that reach past its right edge, e.g. <c>div.ft-chart (1520px)</c>. Empty when it fits.
    /// </summary>
    protected static async Task<List<string>> WhatScrollsSidewaysAsync(IPage page) =>
        [.. await page.EvaluateAsync<string[]>(SidewaysScript)];

    // Nothing when the page fits, otherwise each element past the edge whose parent isn't, by its tag, classes and
    // right edge
    private const string SidewaysScript = """
        () => {
            const width = window.innerWidth;
            if (document.documentElement.scrollWidth <= width) {
                return [];
            }
            const past = element => element.getBoundingClientRect().right > width + 1;
            return [...document.body.querySelectorAll('*')]
                .filter(x => past(x) && !(x.parentElement && x.parentElement !== document.body && past(x.parentElement)))
                .slice(0, 5)
                .map(x => `${x.tagName.toLowerCase()}.${[...x.classList].join('.')} (${Math.round(x.getBoundingClientRect().right)}px)`);
        }
        """;

    /// <summary>
    /// Whether all of <paramref name="locator"/> is on the screen, none of it cut off at a side, the top or the
    /// bottom, e.g. a dialog's buttons below the bottom of a phone.
    /// </summary>
    protected static async Task<bool> FitsOnScreenAsync(IPage page, ILocator locator)
    {
        LocatorBoundingBoxResult? box = await locator.BoundingBoxAsync();
        if (box is null || page.ViewportSize is null)
        {
            return false;
        }

        bool fitsAcross = box.X >= 0 && box.X + box.Width <= page.ViewportSize.Width;
        bool fitsDown = box.Y >= 0 && box.Y + box.Height <= page.ViewportSize.Height;
        return fitsAcross && fitsDown;
    }

    /// <summary>
    /// What axe finds against WCAG 2.1 A and AA on the page as it stands once its transitions have finished, each as
    /// the rule, how bad it is, and the elements it's on, e.g. <c>color-contrast (serious): Elements must meet
    /// minimum color contrast ratio thresholds at .mud-chip (&lt;span class="mud-chip"&gt;…)</c>. Empty when it
    /// finds nothing.
    /// </summary>
    /// <remarks>
    /// Leaves out the rows per page select of MudBlazor's data grid pager, which has no accessible name and no way
    /// to give it one (MudBlazor 9.12). Kept as it is by choice until MudBlazor fixes it.
    /// </remarks>
    protected static async Task<List<string>> AccessibilityProblemsAsync(IPage page)
    {
        // A colour mid-transition, e.g. a button just enabled, is neither colour it's going between
        await page.WaitForFunctionAsync(SettledScript);

        AxeRunContext everythingBut = new()
        {
            Exclude = [new AxeSelector(".mud-table-pagination-select")]
        };
        AxeResult result = await page.RunAxe(everythingBut, new AxeRunOptions
        {
            RunOnly = new RunOnlyOptions { Type = "tag", Values = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"] }
        });

        return result.Violations.Select(Describe).ToList();
    }

    // Whether every animation that ends has ended, a spinner's run for as long as it's there
    private const string SettledScript = """
        () => document.getAnimations()
            .filter(x => x.effect?.getTiming().iterations !== Infinity)
            .every(x => x.playState !== 'running')
        """;

    // Each element by its selector and the start of its HTML, as the selector alone is often only generated ids
    private static string Describe(AxeResultItem violation)
    {
        IEnumerable<string> where = violation.Nodes.Select(static x => $"{x.Target} ({StartOf(x.Html)})");
        return $"{violation.Id} ({violation.Impact}): {violation.Help} at {string.Join(", ", where)}";
    }

    private static string StartOf(string html) => html.Length <= 160 ? html : $"{html[..160]}…";

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
