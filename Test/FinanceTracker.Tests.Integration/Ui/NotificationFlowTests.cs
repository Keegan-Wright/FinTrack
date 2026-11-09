using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// The app bar's bell and the notifications page, as someone with an unread notification uses them: the bell says
/// how many, goes to the page, reading them clears it, and the email settings are kept.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class NotificationFlowTests : UiTestBase
{
    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task Bell_WithOneUnread_GoesToItAndReadingItClearsTheBell(ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(screen, user);
        await GoToAsync(page, "/");
        await WaitUntilLoadedAsync(page);

        // Act
        await Bell(page, "Notifications, 1 unread").ClickAsync();

        // Assert, on the page with it unread
        await Assertions.Expect(page.Locator(".ft-page-header__title")).ToHaveTextAsync("Notifications");
        await Assertions.Expect(page.Locator(".ft-notification--unread")).ToContainTextAsync(SeededUser.Notice);

        // Act
        await Button(page, "Mark all as read").ClickAsync();

        // Assert, nothing unread, the bell says so without a reload
        await Assertions.Expect(page.Locator(".ft-notification--unread")).ToHaveCountAsync(0);
        await Assertions.Expect(Bell(page, "Notifications")).ToBeVisibleAsync();
        await Assert.That(BrowserErrors).IsEmpty();
    }

    [Test]
    public async Task Title_Clicked_GoesToWhereItsDealtWithAndReadsIt()
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await GoToAsync(page, "/Notifications");
        await WaitUntilLoadedAsync(page);

        // Act
        await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = SeededUser.Notice }).ClickAsync();

        // Assert
        await Assertions.Expect(page.Locator(".ft-page-header__title")).ToHaveTextAsync("Bank connections");
        await Assertions.Expect(Bell(page, "Notifications")).ToBeVisibleAsync();
        await Assert.That(BrowserErrors).IsEmpty();
    }

    [Test]
    public async Task EmailSetting_TurnedOff_IsKeptAfterLoadingAgain()
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, "/Notifications/Settings");
        await WaitUntilLoadedAsync(page);
        ILocator syncFailures = page.GetByRole(AriaRole.Checkbox, new PageGetByRoleOptions
        {
            Name = "Email: A bank failing to sync", Exact = true
        });
        await Assertions.Expect(syncFailures.First).ToBeCheckedAsync();

        // Act, waiting for it to be saved, as the box unticks before that
        await page.RunAndWaitForResponseAsync(() => syncFailures.First.ClickAsync(),
            static response => response.Url.EndsWith("/notifications/settings", StringComparison.OrdinalIgnoreCase)
                               && response.Request.Method == "PUT");
        await Assertions.Expect(syncFailures.First).Not.ToBeCheckedAsync();
        await GoToAsync(page, "/Notifications/Settings");
        await WaitUntilLoadedAsync(page);

        // Assert
        await Assertions.Expect(syncFailures.First).Not.ToBeCheckedAsync();
        await Assert.That(BrowserErrors).IsEmpty();
    }

    // In the app bar, the navigation has a Notifications link too
    private static ILocator Bell(IPage page, string name) =>
        page.Locator(".mud-appbar").GetByLabel(name, new LocatorGetByLabelOptions { Exact = true });
}
