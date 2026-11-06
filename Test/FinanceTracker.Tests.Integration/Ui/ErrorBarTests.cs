using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Something breaking outside a page, which can't show the error in the page's place, brings up Blazor's error bar
/// rather than leaving the app stuck without saying so. Here the server answers the menu's when-last-synced with a
/// web page rather than the app's data, as a proxy in front of it might.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class ErrorBarTests : UiTestBase
{
    public static IEnumerable<(ScreenSize Screen, bool Dark)> Screens() =>
        [(ScreenSize.Phone, false), (ScreenSize.Desktop, true)];

    [Test]
    [MethodDataSource(nameof(Screens))]
    public async Task OutsideAPage_SomethingBreaks_SaysSoAccessiblyAndReloadCarriesOn(ScreenSize screen, bool dark)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(screen, user, dark);
        await AnswerLastSyncedWithAWebPageAsync(page);

        // Act
        await GoToAsync(page, "/");

        // Assert, said and announced, over the page
        ILocator bar = page.GetByRole(AriaRole.Alert).Filter(new LocatorFilterOptions
        {
            HasText = "Something went wrong"
        });
        await Assertions.Expect(bar).ToBeVisibleAsync();
        List<string> problems = await AccessibilityProblemsAsync(page);
        await Assert.That(problems).IsEmpty();

        // Act, the server answering as it should again
        await page.UnrouteAllAsync();
        await Button(page, "Reload").ClickAsync();
        await WaitUntilInteractiveAsync(page);

        // Assert
        await Assertions.Expect(page.Locator(".ft-page-header__title")).ToBeVisibleAsync();
        await Assertions.Expect(bar).ToBeHiddenAsync();
    }

    [Test]
    public async Task Dismiss_HidesTheBar()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await AnswerLastSyncedWithAWebPageAsync(page);
        await GoToAsync(page, "/");
        ILocator bar = page.Locator("#blazor-error-ui");
        await Assertions.Expect(bar).ToBeVisibleAsync();

        // Act
        await Button(page, "Dismiss").ClickAsync();

        // Assert
        await Assertions.Expect(bar).ToBeHiddenAsync();
    }

    private static async Task AnswerLastSyncedWithAWebPageAsync(IPage page) =>
        await page.RouteAsync("**/api/sync/last", static route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "text/html",
            Body = "<!DOCTYPE html><html><body>Sign in to continue</body></html>"
        }));
}
