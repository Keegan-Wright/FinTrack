using FinanceTracker.Tests.Integration.Ui;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Image;

/// <summary>
/// The published Docker image, as it ships, in a browser. Every other UI test runs the app built for testing, whose
/// WebAssembly isn't trimmed and whose static files are found differently, so something only a publish gets wrong,
/// such as Blazor's own script missing from the image, would pass them all. Run against the image with
/// <see cref="PublishedImage.AddressVariable"/> set, skipped without it.
/// </summary>
[SkipWithoutImage]
public class PublishedImageTests
{
    private readonly List<string> _browserErrors = [];

    [ClassDataSource<BrowserFixture>(Shared = SharedType.PerTestSession)]
    public required BrowserFixture Browsers { get; init; }

    [Test]
    public async Task Image_SignedUpInTheBrowser_RunsTheAppAndItsPages()
    {
        // Arrange
        await using IBrowserContext context = await Browsers.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = PublishedImage.Address!.ToString(),
            ViewportSize = new ViewportSize { Width = Ui.ScreenSize.Desktop.Width, Height = Ui.ScreenSize.Desktop.Height }
        });
        IPage page = await context.NewPageAsync();
        WatchForErrors(page);
        await page.GotoAsync("Account/Register");

        // Act, a new account each run, the image may be one with users already
        string username = $"image{Guid.NewGuid():N}"[..20];
        await page.GetByLabel("First name").FillAsync("Sam");
        await page.GetByLabel("Last name").FillAsync("Example");
        await page.GetByLabel("Username").FillAsync(username);
        await page.GetByLabel("Email").FillAsync($"{username}@example.test");
        await page.GetByLabel("Password", new PageGetByLabelOptions { Exact = true }).FillAsync("Image-Check-1234");
        await page.GetByLabel("Confirm password").FillAsync("Image-Check-1234");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create account" }).ClickAsync();

        // Assert, the app runs in the browser: the dashboard, then pages through the menu without loading again
        await WaitUntilInteractiveAsync(page);
        await Assertions.Expect(page).ToHaveTitleAsync("FinTrack - Dashboard");
        foreach (string link in (string[])["Transactions", "Bills", "Reports"])
        {
            await page.Locator("#nav-drawer")
                .GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = link, Exact = true })
                .ClickAsync();
            await Assertions.Expect(page.Locator(".ft-page-header__title")).ToHaveTextAsync(link);
        }

        await Assert.That(_browserErrors).IsEmpty();
    }

    private void WatchForErrors(IPage page)
    {
        page.PageError += (_, error) => _browserErrors.Add(error);
        page.Console += (_, message) =>
        {
            if (message.Type == "error" && !message.Text.StartsWith("Failed to load resource", StringComparison.Ordinal))
            {
                _browserErrors.Add(message.Text);
            }
        };
        page.Response += (_, response) =>
        {
            if (response.Status >= 400)
            {
                _browserErrors.Add($"{response.Status} from {response.Request.Method} {response.Url}");
            }
        };
    }

    private static Task WaitUntilInteractiveAsync(IPage page) =>
        page.Locator("[data-interactive]").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = 60_000
        });
}
