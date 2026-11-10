using System.Text.RegularExpressions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account.Email;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Asking for a password reset from the sign in page. It says the same whether or not the email has an account, so
/// it can't be used to find out who has one.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public partial class ForgotPasswordTests : UiTestBase
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

    [Test]
    public async Task ForgotPassword_ConfirmedEmail_EmailsALinkThatSetsANewPassword()
    {
        // Arrange
        const string newPassword = "Brand-New-Password-2!";
        SignedInUser user = await RegisterUserAsync();
        string email = $"{user.Username}@fintrack.test";

        IPage page = await OpenAsync(ScreenSize.Phone);
        await page.GotoAsync("/Account/ForgotPassword");
        await page.GetByLabel("Email").FillAsync(email);
        await Button(page, "Send reset link").ClickAsync();

        // Act
        CaughtMail mail = (await App.Mail.WaitForAsync(email, AccountEmails.ResetPasswordSubject, CancellationToken))
            .Single();
        string link = ResetLink().Match(mail.Text).Groups["link"].Value;

        await page.GotoAsync(link);
        await page.GetByLabel("New password", new PageGetByLabelOptions { Exact = true }).FillAsync(newPassword);
        await page.GetByLabel("Confirm new password").FillAsync(newPassword);
        await Button(page, "Reset password").ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Level = 1 }))
            .ToHaveTextAsync("Password reset");

        await page.GotoAsync("/Account/Login");
        await page.GetByLabel("Username").FillAsync(user.Username);
        await page.GetByLabel("Password", new PageGetByLabelOptions { Exact = true }).FillAsync(newPassword);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sign in", Exact = true })
            .ClickAsync();
        await WaitUntilInteractiveAsync(page);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(new Uri(page.Url).AbsolutePath).IsEqualTo("/");
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    public async Task ForgotPassword_AccountWithoutAPassword_SendsNothing()
    {
        // Arrange, as one made by signing in through the identity provider, whose password is the provider's
        SignedInUser user = await RegisterUserAsync();
        string email = $"{user.Username}@fintrack.test";
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            await context.Users.Where(x => x.Id == user.UserId)
                .ExecuteUpdateAsync(x => x.SetProperty(u => u.PasswordHash, (string?)null), CancellationToken);
        }

        IPage page = await OpenAsync(ScreenSize.Phone);
        await page.GotoAsync("/Account/ForgotPassword");

        // Act
        await page.GetByLabel("Email").FillAsync(email);
        await Button(page, "Send reset link").ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Level = 1 }))
            .ToHaveTextAsync("Check your email");

        // Assert, the same page as when one's sent, so it doesn't say which accounts have a password
        await Assert.That(await App.Mail.HasAnyAsync(email, AccountEmails.ResetPasswordSubject, CancellationToken)).IsFalse();
    }

    [GeneratedRegex(@"Choose a new password: (?<link>\S+)")]
    private static partial Regex ResetLink();
}
