using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Creating an account with the register form, as someone new to the app does.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class RegisterTests : UiTestBase
{
    private const string Password = "Placeholder-Pa55word!";

    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task Register_WithTheForm_LandsOnTheDashboard(ScreenSize screen)
    {
        // Arrange
        IPage page = await OpenAsync(screen);
        await page.GotoAsync("/Account/Register");

        // Act
        await FillInAsync(page, UniqueName(), Password, Password);
        await Button(page, "Create account").ClickAsync();
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
    public async Task Register_WithPasswordsThatDontMatch_SaysSoAndStaysOnTheForm()
    {
        // Arrange
        IPage page = await OpenAsync(ScreenSize.Phone);
        await page.GotoAsync("/Account/Register");

        // Act
        await FillInAsync(page, UniqueName(), Password, Password + "-different");
        await Button(page, "Create account").ClickAsync();

        // Assert
        await Assertions.Expect(page.GetByText("Passwords do not match")).ToBeVisibleAsync();
        using (Assert.Multiple())
        {
            await Assert.That(new Uri(page.Url).AbsolutePath).IsEqualTo("/Account/Register");
            await Assert.That(await AccessibilityProblemsAsync(page)).IsEmpty();
        }
    }

    private static async Task FillInAsync(IPage page, string username, string password, string confirmPassword)
    {
        await page.GetByLabel("First name").FillAsync("Alex");
        await page.GetByLabel("Last name").FillAsync("Example");
        await page.GetByLabel("Username").FillAsync(username);
        await page.GetByLabel("Email").FillAsync($"{username}@fintrack.test");
        await page.GetByLabel("Password", new PageGetByLabelOptions { Exact = true }).FillAsync(password);
        await page.GetByLabel("Confirm password").FillAsync(confirmPassword);
    }
}
