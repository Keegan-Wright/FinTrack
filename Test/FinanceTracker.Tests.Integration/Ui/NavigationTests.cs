using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Getting around with the side menu, docked beside the page on a desktop and opened over it from the app bar on a
/// phone or tablet.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class NavigationTests : UiTestBase
{
    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task Menu_GoesToTheChosenPage(ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(screen, user);
        await GoToAsync(page, "/");

        // Act
        if (screen.DrawerOverPage)
        {
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Toggle navigation" })
                .ClickAsync();
        }

        await page.Locator("#nav-drawer").GetByRole(AriaRole.Link, new LocatorGetByRoleOptions
        {
            Name = "Transactions", Exact = true
        }).ClickAsync();

        // Assert
        ILocator header = page.Locator(".ft-page-header__title");
        await Expect(header).ToHaveTextAsync("Transactions");
        using (Assert.Multiple())
        {
            await Assert.That(new Uri(page.Url).AbsolutePath).IsEqualTo("/Transactions");
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    private static ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);
}
