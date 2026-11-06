using System.Text.RegularExpressions;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

[ParallelLimiter<UiParallelLimit>]
public partial class SignInTests : UiTestBase
{
    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task SignIn_WithTheForm_LandsOnTheDashboard(ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(screen);
        await page.GotoAsync("/Account/Login");

        // Act
        await page.GetByLabel("Username").FillAsync(user.Username);
        await page.GetByLabel("Password", new PageGetByLabelOptions { Exact = true })
            .FillAsync(SignedInUser.DefaultPassword);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sign in", Exact = true })
            .ClickAsync();
        await WaitUntilInteractiveAsync(page);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(new Uri(page.Url).AbsolutePath).IsEqualTo("/");
            await Assert.That(await page.Locator(".ft-page-header__title").IsVisibleAsync()).IsTrue();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    public async Task SignedOut_GoingToAPage_IsSentToSignIn()
    {
        // Arrange
        IPage page = await OpenAsync(ScreenSize.Desktop);

        // Act
        await page.GotoAsync("/Transactions");

        // Assert
        await Assert.That(page.Url).Matches(LoginPage());
    }

    [GeneratedRegex("/Account/Login", RegexOptions.IgnoreCase)]
    private static partial Regex LoginPage();
}
