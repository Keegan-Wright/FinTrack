using FinanceTracker.Design;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// What the app bar does beside the navigation: signing out from the account menu, and switching between light and
/// dark mode.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class AppBarTests : UiTestBase
{
    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task SignOut_FromTheAccountMenu_SendsPagesToSignIn(ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(screen, user);
        await GoToAsync(page, "/");

        // Act
        await Button(page, "Account").ClickAsync();
        await Button(page, "Sign out").ClickAsync();
        await page.WaitForURLAsync("**/Account/Login**");
        await page.GotoAsync("/Transactions");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(new Uri(page.Url).AbsolutePath).IsEqualTo("/Account/Login");
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    public async Task SignOut_InAnotherTab_NextPageGoesToSignInThenBackToIt()
    {
        // Arrange, the app open in two tabs
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await GoToAsync(page, "/");
        await WaitUntilLoadedAsync(page);
        IPage otherTab = await page.Context.NewPageAsync();
        await GoToAsync(otherTab, "/");

        // Act, signed out in the other, then on to a page in this one, which the app here still thinks is fine
        await Button(otherTab, "Account").ClickAsync();
        await Button(otherTab, "Sign out").ClickAsync();
        await otherTab.WaitForURLAsync("**/Account/Login**");
        await page.Locator("#nav-drawer")
            .GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Bills", Exact = true })
            .ClickAsync();

        // Assert, sent to sign in
        await page.WaitForURLAsync("**/Account/Login**");

        // Act, signing in again
        await page.GetByLabel("Username").FillAsync(user.Username);
        await page.GetByLabel("Password", new PageGetByLabelOptions { Exact = true })
            .FillAsync(SignedInUser.DefaultPassword);
        await Button(page, "Sign in").ClickAsync();
        await WaitUntilInteractiveAsync(page);

        // Assert, back on the page they went to, the server only refusing while they were signed out
        await Assertions.Expect(page.Locator(".ft-page-header__title")).ToHaveTextAsync("Bills");
        using (Assert.Multiple())
        {
            await Assert.That(new Uri(page.Url).AbsolutePath).IsEqualTo("/Bills");
            await Assert.That(BrowserErrors.Where(static x => !x.StartsWith("401 from", StringComparison.Ordinal)))
                .IsEmpty();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DarkModeToggle_SwitchesToTheOtherModesColours(bool startDark)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user, dark: startDark);
        await GoToAsync(page, "/");
        string backgroundBefore = await BackgroundAsync(page);

        // Act
        await Button(page, "Toggle dark mode").ClickAsync();

        // Assert, the money colours follow the mode as MudBlazor's palette does
        string moneyIn = startDark ? MoneyColours.LightMoneyIn : MoneyColours.DarkMoneyIn;
        await page.WaitForFunctionAsync($"expected => {MoneyInScript} === expected", moneyIn);
        using (Assert.Multiple())
        {
            await Assert.That(await page.EvaluateAsync<string>($"() => {MoneyInScript}")).IsEqualTo(moneyIn);
            await Assert.That(await BackgroundAsync(page)).IsNotEqualTo(backgroundBefore);
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    // The money in colour the page is styled with, see MoneyColourStyles
    private const string MoneyInScript =
        "getComputedStyle(document.documentElement).getPropertyValue('--ft-money-in').trim()";

    // The page's background as the browser draws it, e.g. rgb(10, 10, 10)
    private static Task<string> BackgroundAsync(IPage page) =>
        page.EvaluateAsync<string>("() => getComputedStyle(document.body).backgroundColor");
}
