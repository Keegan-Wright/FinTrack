using Microsoft.Playwright;
using TUnit.Core.Interfaces;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// One Chromium for the whole test session, each test opening its own context in it, see
/// <see cref="UiTestBase"/>. Headless unless <c>FINTRACK_UI_HEADED</c> is set, to watch the tests run.
/// </summary>
public sealed class BrowserFixture : IAsyncInitializer, IAsyncDisposable
{
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Downloads Chromium the first time, a no-op once it's there. CI installs it first, with the system
        // libraries it needs
        int installed = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (installed != 0)
        {
            throw new InvalidOperationException($"Installing Chromium for Playwright exited with {installed}");
        }

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = Environment.GetEnvironmentVariable("FINTRACK_UI_HEADED") is null
        });

        // Playwright's 5 seconds for an expectation is short with several browsers loading from the one app at once
        Assertions.SetDefaultExpectTimeout(ExpectTimeout);
    }

    /// <summary>
    /// How long an expectation, e.g. <c>ToBeVisibleAsync</c>, waits for the page to get there.
    /// </summary>
    public const float ExpectTimeout = 15_000;

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }

        _playwright?.Dispose();
    }
}
