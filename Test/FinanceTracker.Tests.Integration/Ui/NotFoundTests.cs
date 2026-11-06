using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// An address that isn't a page, typed in or linked to, or followed inside the app, says so in the app with its
/// menu working, and leads back to the dashboard. The server still answers 404.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class NotFoundTests : UiTestBase
{
    private const string Missing = "/no-such-page";

    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task Address_ThatIsntAPage_SaysSoAccessiblyAndAnswers404(ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(screen, user);

        // Act
        IResponse? response = await page.GotoAsync(Missing);
        await WaitUntilInteractiveAsync(page);

        // Assert, at the address asked for
        await Assertions.Expect(page.Locator(".ft-page-header__title")).ToHaveTextAsync("Page not found");
        using (Assert.Multiple())
        {
            await Assert.That(response?.Status).IsEqualTo(404);
            await Assert.That(new Uri(page.Url).AbsolutePath).IsEqualTo(Missing);
            await Assert.That(await page.TitleAsync()).IsEqualTo("FinTrack - Page not found");
            await Assert.That(await WhatScrollsSidewaysAsync(page)).IsEmpty();
            await Assert.That(await AccessibilityProblemsAsync(page)).IsEmpty();
            await Assert.That(UnexpectedErrors()).IsEmpty();
        }
    }

    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task NotFound_TheMenu_StillGoesToAPage(ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(screen, user);
        await page.GotoAsync(Missing);
        await WaitUntilInteractiveAsync(page);

        // Act
        if (screen.DrawerOverPage)
        {
            await Button(page, "Toggle navigation").ClickAsync();
        }

        await page.Locator("#nav-drawer").GetByRole(AriaRole.Link, new LocatorGetByRoleOptions
        {
            Name = "Bills",
            Exact = true
        }).ClickAsync();

        // Assert
        await Assertions.Expect(page.Locator(".ft-page-header__title")).ToHaveTextAsync("Bills");
        await Assert.That(UnexpectedErrors()).IsEmpty();
    }

    [Test]
    public async Task NotFound_GoToYourDashboard_GoesThere()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await page.GotoAsync(Missing);
        await WaitUntilInteractiveAsync(page);

        // Act
        await page.Locator("a[href]").Filter(new LocatorFilterOptions { HasText = "Go to your dashboard" })
            .ClickAsync();

        // Assert
        await Assertions.Expect(page.Locator(".ft-page-header__title")).Not.ToHaveTextAsync("Page not found");
        await Assert.That(new Uri(page.Url).AbsolutePath).IsEqualTo("/");
    }

    [Test]
    public async Task InTheApp_FollowingALinkThatIsntAPage_SaysSo()
    {
        // Arrange, the app running in the browser
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await GoToAsync(page, "/");
        await WaitUntilLoadedAsync(page);

        // Act, as a link in the app would, without loading the page
        await page.EvaluateAsync($"() => Blazor.navigateTo('{Missing}')");

        // Assert
        await Assertions.Expect(page.Locator(".ft-page-header__title")).ToHaveTextAsync("Page not found");
        using (Assert.Multiple())
        {
            await Assert.That(new Uri(page.Url).AbsolutePath).IsEqualTo(Missing);
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    public async Task SignedOut_AnAddressThatIsntAPage_SignsInFirst()
    {
        // Arrange
        IPage page = await OpenAsync(ScreenSize.Desktop);

        // Act
        await page.GotoAsync(Missing);

        // Assert
        await page.WaitForURLAsync("**/Account/Login**");
    }

    /// <summary>
    /// What went wrong in the browser besides the page's own 404.
    /// </summary>
    private List<string> UnexpectedErrors() =>
        BrowserErrors.Where(static x => !x.StartsWith("404 from GET", StringComparison.Ordinal)
                                        || !x.EndsWith(Missing, StringComparison.Ordinal)).ToList();
}
