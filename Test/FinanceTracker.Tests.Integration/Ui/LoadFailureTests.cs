using System.Text.RegularExpressions;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// A page whose data can't be loaded, e.g. the server is down, says so in its place rather than leaving a blank or
/// broken page, the menu still works, and trying again loads it once the server answers. The app is already running
/// in the browser, so pages load their data through the API, which answers 500 here.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class LoadFailureTests : UiTestBase
{
    private const string Failed = "Something went wrong";

    /// <summary>
    /// Each page by its link in the menu.
    /// </summary>
    public static IEnumerable<string> Links() =>
    [
        "Accounts", "Transactions", "Calendar", "Budgets", "Bills", "Debts", "Savings goals", "Net worth", "Reports",
        "Notifications", "Household", "Tags", "Rules", "Your data"
    ];

    [Test]
    [MethodDataSource(nameof(Links))]
    public async Task Page_ItsDataFailsToLoad_SaysSoAndLoadsWhenTriedAgain(string link)
    {
        // Arrange, the app running in the browser, then the server failing
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await GoToAsync(page, "/");
        await WaitUntilLoadedAsync(page);
        await page.RouteAsync("**/api/**", static route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 500
        }));

        // Act
        await MenuLink(page, link).ClickAsync();

        // Assert, said in the page's place, nothing left loading
        ILocator header = page.Locator(".ft-page-header__title");
        await Assertions.Expect(header).ToHaveTextAsync(Failed);
        await WaitUntilLoadedAsync(page);
        List<string> problems = await AccessibilityProblemsAsync(page);

        // Act, the server's back
        await page.UnrouteAllAsync();
        await Button(page, "Try again").ClickAsync();

        // Assert, the page itself
        await Assertions.Expect(header).Not.ToHaveTextAsync(Failed);
        await Assertions.Expect(page.GetByText(Failed).Filter(new LocatorFilterOptions { Visible = true }))
            .ToHaveCountAsync(0);
        using (Assert.Multiple())
        {
            await Assert.That(problems).IsEmpty();
            await Assert.That(UnexpectedErrors()).IsEmpty();
        }
    }

    [Test]
    public async Task Menu_AfterAPageFailedToLoad_GoesToTheNextPage()
    {
        // Arrange, one page failed
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await GoToAsync(page, "/");
        await WaitUntilLoadedAsync(page);
        await page.RouteAsync("**/api/**", static route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 500
        }));
        await MenuLink(page, "Bills").ClickAsync();
        ILocator header = page.Locator(".ft-page-header__title");
        await Assertions.Expect(header).ToHaveTextAsync(Failed);
        await page.UnrouteAllAsync();

        // Act
        await MenuLink(page, "Debts").ClickAsync();

        // Assert, the error went with the page
        await Assertions.Expect(header).ToHaveTextAsync("Debts");
        await Assertions.Expect(page.GetByText(SeededUser.Debt).First).ToBeVisibleAsync();
    }

    // By the page's name, then perhaps a count, e.g. "Notifications 1 unread"
    private static ILocator MenuLink(IPage page, string link) =>
        page.Locator("#nav-drawer").GetByRole(AriaRole.Link, new LocatorGetByRoleOptions
        {
            NameRegex = new Regex($"^{Regex.Escape(link)}( |$)")
        });

    /// <summary>
    /// What went wrong in the browser besides the 500s asked for, and the error boundary logging what it caught.
    /// </summary>
    private List<string> UnexpectedErrors() =>
        BrowserErrors.Where(static x => !IsExpected(x)).ToList();

    private static bool IsExpected(string error) =>
        error.StartsWith("500 from", StringComparison.Ordinal)
        || error.StartsWith("fail: Microsoft.AspNetCore.Components.Web.ErrorBoundary", StringComparison.Ordinal);
}
