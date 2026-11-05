using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Every page a signed in user can reach, run in the browser on each screen: it starts without errors, shows its
/// header, and fits the screen. A page can answer 200 and still fail once it runs in the browser, which only a browser
/// shows.
/// </summary>
public class PageRenderTests : UiTestBase
{
    /// <summary>
    /// Each page's path and its header's title, null for the dashboard, whose title greets the user by the time of
    /// day.
    /// </summary>
    private static readonly (string Path, string? Title)[] Pages =
    [
        ("/", null),
        ("/Accounts", "Accounts"),
        ("/Transactions", "Transactions"),
        ("/Calendar", "Calendar"),
        ("/Budgets", "Budgets"),
        ("/Bills", "Bills"),
        ("/Debts", "Debts"),
        ("/SavingsGoals", "Savings goals"),
        ("/NetWorth", "Net worth"),
        ("/Reports", "Reports"),
        ("/Reports/accounts", "Reports"),
        ("/Reports/tags", "Reports"),
        ("/Review", "Review transfers"),
        ("/Review/Subscriptions", "Review subscriptions"),
        ("/Classifications", "Tags"),
        ("/Rules", "Rules"),
        ("/Rules/new", null),
        ("/HouseholdMembers", "Household"),
        ("/ProviderSetup", "Bank connections"),
        ("/YourData", "Your data")
    ];

    public static IEnumerable<(string Path, string? Title, ScreenSize Screen)> PagesOnEachScreen() =>
        Pages.SelectMany(static page => ScreenSize.All.Select(screen => (page.Path, page.Title, screen)));

    [Test]
    [MethodDataSource(nameof(PagesOnEachScreen))]
    public async Task Page_RunsInTheBrowserAndFitsTheScreen(string path, string? title, ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(screen, user);

        // Act
        await GoToAsync(page, path);

        // Assert
        ILocator header = page.Locator(".ft-page-header__title");
        await header.WaitForAsync();
        using (Assert.Multiple())
        {
            if (title is not null)
            {
                await Assert.That((await header.InnerTextAsync()).Trim()).IsEqualTo(title);
            }

            await Assert.That(BrowserErrors).IsEmpty();
            await Assert.That(await ScrollsSidewaysAsync(page)).IsFalse();
        }
    }
}
