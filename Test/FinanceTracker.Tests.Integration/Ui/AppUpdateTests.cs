using FinanceTracker.Contracts.Api;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// FinTrack updated while a tab has it open, the tab says so and carries on, and reloads only when the user asks.
/// The server's answers say it runs another build than the tab's, as they would after an update.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class AppUpdateTests : UiTestBase
{
    private const string NewerBuild = "999.0.0+0000000";

    public static IEnumerable<(ScreenSize Screen, bool Dark)> Screens() =>
        [(ScreenSize.Phone, false), (ScreenSize.Desktop, true)];

    [Test]
    [MethodDataSource(nameof(Screens))]
    public async Task Update_WhileOpen_SaysSoAccessiblyAndReloadGetsIt(ScreenSize screen, bool dark)
    {
        // Arrange, the app open, then updated on the server
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(screen, user, dark);
        await GoToAsync(page, "/");
        await WaitUntilLoadedAsync(page);
        ILocator banner = page.Locator(".ft-update-banner");
        await Assertions.Expect(banner).ToHaveCountAsync(0);
        await page.RouteAsync("**/api/**", static route => AsAnotherBuildAsync(route));

        // Act, on to another page, which still works
        if (screen.DrawerOverPage)
        {
            await Button(page, "Toggle navigation").ClickAsync();
        }

        await page.Locator("#nav-drawer").GetByRole(AriaRole.Link, new LocatorGetByRoleOptions
        {
            Name = "Bills",
            Exact = true
        }).ClickAsync();
        await WaitUntilLoadedAsync(page);

        // Assert
        await Assertions.Expect(banner).ToContainTextAsync("FinTrack has been updated");
        await Assertions.Expect(page.GetByText(SeededUser.Bill).First).ToBeVisibleAsync();
        List<string> problems = await AccessibilityProblemsAsync(page);

        // Act, the tab reloaded, now running the server's build
        await page.UnrouteAllAsync();
        await page.GetByText("Reload to get the latest").ClickAsync();
        await WaitUntilInteractiveAsync(page);
        await WaitUntilLoadedAsync(page);

        // Assert
        await Assertions.Expect(banner).ToHaveCountAsync(0);
        using (Assert.Multiple())
        {
            await Assert.That(new Uri(page.Url).AbsolutePath).IsEqualTo("/Bills");
            await Assert.That(problems).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    /// <summary>
    /// The server's own answer, saying it runs <see cref="NewerBuild"/>.
    /// </summary>
    private static async Task AsAnotherBuildAsync(IRoute route)
    {
        IAPIResponse response = await route.FetchAsync();
        Dictionary<string, string> headers = new(response.Headers, StringComparer.OrdinalIgnoreCase)
        {
            [ApiHeaders.Version] = NewerBuild
        };
        await route.FulfillAsync(new RouteFulfillOptions { Response = response, Headers = headers });
    }
}
