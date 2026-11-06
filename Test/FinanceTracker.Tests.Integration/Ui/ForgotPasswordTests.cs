using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Asking for a password reset from the sign in page. It says the same whether or not the email has an account, so
/// it can't be used to find out who has one.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class ForgotPasswordTests : UiTestBase
{
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ForgotPassword_FromSignIn_SaysToCheckYourEmailWhetherOrNotItHasAnAccount(bool hasAnAccount)
    {
        // Arrange
        string email = $"{UniqueName()}@fintrack.test";
        if (hasAnAccount)
        {
            SignedInUser user = await RegisterUserAsync();
            email = $"{user.Username}@fintrack.test";
        }

        IPage page = await OpenAsync(ScreenSize.Phone);
        await page.GotoAsync("/Account/Login");

        // Act
        await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Forgot password?" }).ClickAsync();
        await page.GetByLabel("Email").FillAsync(email);
        await Button(page, "Send reset link").ClickAsync();

        // Assert
        ILocator heading = page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Level = 1 });
        await Assertions.Expect(heading).ToHaveTextAsync("Check your email");
        using (Assert.Multiple())
        {
            await Assert.That(new Uri(page.Url).AbsolutePath).IsEqualTo("/Account/ForgotPasswordConfirmation");
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }
}
