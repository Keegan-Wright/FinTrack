using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// A tab bar too narrow for its tabs, as on a phone, keeps the chosen tab in view. A tab taking focus, e.g. from the
/// arrow keys, used to move the tabs twice and leave the bar blank.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class TabBarTests : UiTestBase
{
    [Test]
    [Arguments("Webhook")]
    [Arguments("Push")]
    public async Task Phone_LaterTabTapped_StaysInTheBar(string tab)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, "/Notifications/Settings");
        await WaitUntilLoadedAsync(page);

        // Act, Webhook only ever partly in view
        await ChooseTabAsync(page, tab);

        // Assert, once the bar has slid along
        await page.WaitForTimeoutAsync(1000);
        ILocator chosen = page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = tab, Exact = true });
        using (Assert.Multiple())
        {
            await Assert.That(await TabInViewAsync(page, chosen)).IsGreaterThan(0);
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    public async Task Phone_KeyboardToALaterTab_StaysInTheBar()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, "/Notifications/Settings");
        await WaitUntilLoadedAsync(page);
        await page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = "What and where", Exact = true })
            .FocusAsync();

        // Act, along to Webhook with the arrow keys
        for (int i = 0; i < 3; i++)
        {
            await page.Keyboard.PressAsync("ArrowRight");
            await page.WaitForTimeoutAsync(300);
        }

        await page.Keyboard.PressAsync("Enter");

        // Assert
        ILocator chosen = page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = "Webhook", Exact = true });
        await Assertions.Expect(chosen).ToHaveAttributeAsync("aria-selected", "true");
        await page.WaitForTimeoutAsync(1000);
        await Assert.That(await TabInViewAsync(page, chosen)).IsGreaterThan(0);
    }
}
