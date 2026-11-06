using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// The pages someone signed out sees, on each screen: they fit, show their heading, and axe finds nothing against
/// WCAG 2.1 AA. They're server rendered in the dark theme whatever the system's setting, see <c>AccountLayout</c>,
/// so there's no light mode to check.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class AccountPageTests : UiTestBase
{
    private static readonly (string Path, string Heading)[] Pages =
    [
        ("/Account/Login", "Sign in"),
        ("/Account/Register", "Create an account"),
        ("/Account/ForgotPassword", "Forgot your password?"),
        ("/Account/ForgotPasswordConfirmation", "Check your email"),
        // Any code that decodes shows the form, it's only checked once a new password is sent. "placeholder" here
        ("/Account/ResetPassword?code=cGxhY2Vob2xkZXI", "Choose a new password"),
        ("/Account/ResetPasswordConfirmation", "Password reset"),
        ("/Account/InvalidPasswordReset", "That reset link isn't valid"),
        ("/Account/AccessDenied", "Access denied"),
        ("/Account/Lockout", "Account locked"),
        ("/Account/InvalidUser", "We couldn't find that account")
    ];

    public static IEnumerable<(string Path, string Heading, ScreenSize Screen)> PagesOnEachScreen() =>
        Pages.SelectMany(static page => ScreenSize.All.Select(screen => (page.Path, page.Heading, screen)));

    [Test]
    [MethodDataSource(nameof(PagesOnEachScreen))]
    public async Task Page_SignedOut_FitsTheScreenAndIsAccessible(string path, string heading, ScreenSize screen)
    {
        // Arrange
        IPage page = await OpenAsync(screen);

        // Act
        await page.GotoAsync(path);

        // Assert
        ILocator title = page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Level = 1 });
        using (Assert.Multiple())
        {
            await Assert.That((await title.InnerTextAsync()).Trim()).IsEqualTo(heading);
            await Assert.That(BrowserErrors).IsEmpty();
            await Assert.That(await WhatScrollsSidewaysAsync(page)).IsEmpty();
            await Assert.That(await AccessibilityProblemsAsync(page)).IsEmpty();
        }
    }

    /// <summary>
    /// The pages saying how something went, with where to go next. They render on the server only, never run in the
    /// browser, so each way on has to be a link: a button there does nothing.
    /// </summary>
    [Test]
    [Arguments("/Account/ForgotPasswordConfirmation")]
    [Arguments("/Account/ResetPasswordConfirmation")]
    [Arguments("/Account/InvalidPasswordReset")]
    [Arguments("/Account/AccessDenied")]
    [Arguments("/Account/Lockout")]
    [Arguments("/Account/InvalidUser")]
    [Arguments("/Error")]
    public async Task OutcomePage_EachWayOn_IsALink(string path)
    {
        // Arrange
        IPage page = await OpenAsync(ScreenSize.Phone);

        // Act
        await page.GotoAsync(path);

        // Assert
        ILocator message = page.Locator(".ft-auth-message");
        await Assertions.Expect(message).ToBeVisibleAsync();
        using (Assert.Multiple())
        {
            await Assert.That(await message.Locator("a[href]").CountAsync()).IsGreaterThan(0);
            await Assert.That(await message.Locator("button").CountAsync()).IsEqualTo(0);
        }
    }

    /// <summary>
    /// A link without its code, or with one cut short or changed so it doesn't decode.
    /// </summary>
    [Test]
    [Arguments("/Account/ResetPassword")]
    [Arguments("/Account/ResetPassword?code=placeholder")]
    public async Task ResetPassword_WithoutAWholeCode_SaysTheLinkIsntValid(string link)
    {
        // Arrange
        IPage page = await OpenAsync(ScreenSize.Phone);

        // Act
        await page.GotoAsync(link);

        // Assert
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Level = 1 }))
            .ToHaveTextAsync("That reset link isn't valid");
        await Assert.That(new Uri(page.Url).AbsolutePath).IsEqualTo("/Account/InvalidPasswordReset");
    }
}
