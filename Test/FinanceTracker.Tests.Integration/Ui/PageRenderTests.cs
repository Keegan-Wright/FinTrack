using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Every page a signed in user can reach, run in the browser on each screen: it starts without errors, shows its
/// header, fits the screen, and axe finds nothing against WCAG 2.1 AA. A page can answer 200 and still fail once it runs in the browser, which only a browser
/// shows.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
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
        ("/Notifications", "Notifications"),
        ("/YourData", "Your data")
    ];

    public static IEnumerable<(string Path, string? Title, ScreenSize Screen)> PagesOnEachScreen() =>
        Pages.SelectMany(static page => ScreenSize.All.Select(screen => (page.Path, page.Title, screen)));

    public static IEnumerable<string> PagePaths() => Pages.Select(static x => x.Path);

    [Test]
    [MethodDataSource(nameof(PagesOnEachScreen))]
    public async Task Page_RunsInTheBrowserFitsTheScreenAndIsAccessible(string path, string? title, ScreenSize screen)
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
            await Assert.That(await WhatScrollsSidewaysAsync(page)).IsEmpty();
            await Assert.That(await AccessibilityProblemsAsync(page)).IsEmpty();
        }
    }

    /// <summary>
    /// The dark palette is a set of colours of its own, so each page is checked in it too, on a desktop as the
    /// colours don't change with the screen.
    /// </summary>
    [Test]
    [MethodDataSource(nameof(PagePaths))]
    public async Task Page_InDarkMode_IsAccessible(string path)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user, dark: true);

        // Act
        await GoToAsync(page, path);
        await page.Locator(".ft-page-header__title").WaitForAsync();

        // Assert
        await Assert.That(await AccessibilityProblemsAsync(page)).IsEmpty();
    }

    /// <summary>
    /// What the exception handler shows, rendered on the server only and never run in the browser, so it isn't
    /// waited on to become interactive as the other pages are, and its way on has to work without the app.
    /// </summary>
    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task ErrorPage_FitsTheScreenIsAccessibleAndLeadsBackToTheApp(ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(screen, user);

        // Act
        await page.GotoAsync("/Error");

        // Assert
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Level = 1 }))
            .ToHaveTextAsync("Something went wrong");
        List<string> sideways = await WhatScrollsSidewaysAsync(page);
        List<string> problems = await AccessibilityProblemsAsync(page);

        // Act
        // A plain link, so it works without the app, styled as MudBlazor's button
        await page.Locator("a[href]").Filter(new LocatorFilterOptions { HasText = "Back to FinTrack" }).ClickAsync();
        await WaitUntilInteractiveAsync(page);

        // Assert, on the dashboard
        await Assertions.Expect(page.Locator(".ft-page-header__title")).ToBeVisibleAsync();
        using (Assert.Multiple())
        {
            await Assert.That(new Uri(page.Url).AbsolutePath).IsEqualTo("/");
            await Assert.That(sideways).IsEmpty();
            await Assert.That(problems).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }
}
