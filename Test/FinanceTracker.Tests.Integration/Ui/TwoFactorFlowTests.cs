using FinanceTracker.Tests.Integration.Auth;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Turning on two-factor sign in from Your profile with the key the page shows, then signing in with a code, as
/// someone with an authenticator app would.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class TwoFactorFlowTests : UiTestBase
{
    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task TurnOn_FromYourProfile_ThenSignsInWithACode(ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(screen, user);
        await GoToAsync(page, "/YourProfile");
        await page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = "Two-factor" }).ClickAsync();

        // Act, set it up with the key shown
        await Button(page, "Set up two-factor sign in").ClickAsync();
        string key = await page.Locator(".ft-two-factor__key").InnerTextAsync();
        List<string> setupProblems = await AccessibilityProblemsAsync(page);
        List<string> setupTooWide = await WhatScrollsSidewaysAsync(page);
        await page.GetByLabel("Code", new PageGetByLabelOptions { Exact = true }).FillAsync(AuthenticatorApp.Code(key));
        await page.GetByLabel("Your password").FillAsync(SignedInUser.DefaultPassword);
        await Button(page, "Turn on").ClickAsync();
        await Assertions.Expect(page.Locator(".ft-two-factor__codes li")).ToHaveCountAsync(10);
        List<string> codesTooWide = await WhatScrollsSidewaysAsync(page);
        await Button(page, "I've saved them").ClickAsync();

        // And sign in again elsewhere
        IPage signIn = await OpenAsync(screen);
        await signIn.GotoAsync("/Account/Login");
        await signIn.GetByLabel("Username").FillAsync(user.Username);
        await signIn.GetByLabel("Password", new PageGetByLabelOptions { Exact = true })
            .FillAsync(SignedInUser.DefaultPassword);
        await Button(signIn, "Sign in").ClickAsync();
        await signIn.WaitForURLAsync("**/Account/LoginWith2fa**");
        List<string> codePageProblems = await AccessibilityProblemsAsync(signIn);
        // The next step's code, as the one used to turn it on is taken
        await signIn.GetByLabel("Code", new PageGetByLabelOptions { Exact = true })
            .FillAsync(AuthenticatorApp.Code(key, stepsFromNow: 1));
        await Button(signIn, "Sign in").ClickAsync();

        // Assert
        await signIn.WaitForURLAsync(url => !url.Contains("/Account/", StringComparison.OrdinalIgnoreCase));
        await Assertions.Expect(page.GetByText("also asks for a code from your app")).ToBeVisibleAsync();
        using (Assert.Multiple())
        {
            await Assert.That(setupProblems).IsEmpty();
            await Assert.That(setupTooWide).IsEmpty();
            await Assert.That(codesTooWide).IsEmpty();
            await Assert.That(codePageProblems).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }
}
