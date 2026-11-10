using System.Text.RegularExpressions;
using FinanceTracker.Contracts.Profile;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account.Email;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// The user changing their own password and email from their profile, reached from the account menu.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public partial class ProfileFlowTests : UiTestBase
{
    private const string NewPassword = "Brand-New-Password-3!";

    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task ChangePassword_FromTheAccountMenu_SignsInWithTheNewOneAndStaysSignedIn(ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(screen, user);
        await GoToAsync(page, "/");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Account" }).ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions { Name = "Your profile" }).ClickAsync();
        await Assertions.Expect(page.Locator(".ft-page-header__title")).ToHaveTextAsync("Your profile");

        // Act
        await page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = "Password" }).ClickAsync();
        await page.GetByLabel("Current password").FillAsync(SignedInUser.DefaultPassword);
        await page.GetByLabel("New password", new PageGetByLabelOptions { Exact = true }).FillAsync(NewPassword);
        await page.GetByLabel("Confirm new password").FillAsync(NewPassword);
        await Button(page, "Change password").ClickAsync();

        // Assert
        await Assertions.Expect(page.Locator(".mud-snackbar")).ToContainTextAsync("Password changed");
        await GoToAsync(page, "/YourProfile");
        await Assertions.Expect(page.Locator(".ft-page-header__title")).ToHaveTextAsync("Your profile");
        using (Assert.Multiple())
        {
            await Assert.That(await SignsInAsync(user, NewPassword)).IsTrue();
            await Assert.That(await SignsInAsync(user, SignedInUser.DefaultPassword)).IsFalse();
            await Assert.That(await AccessibilityProblemsAsync(page)).IsEmpty();
            await Assert.That(await WhatScrollsSidewaysAsync(page)).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    public async Task ChangePassword_WrongCurrentOne_SaysSo()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, "/YourProfile");
        await page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = "Password" }).ClickAsync();

        // Act
        await page.GetByLabel("Current password").FillAsync("Not-The-Password-1!");
        await page.GetByLabel("New password", new PageGetByLabelOptions { Exact = true }).FillAsync(NewPassword);
        await page.GetByLabel("Confirm new password").FillAsync(NewPassword);
        await Button(page, "Change password").ClickAsync();

        // Assert
        await Assertions.Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Your current password isn't right");
        await Assert.That(await SignsInAsync(user, SignedInUser.DefaultPassword)).IsTrue();
    }

    [Test]
    public async Task ChangeEmail_ByTheLinkSentToTheNewOne_MovesTheAccountToIt()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        string newEmail = $"{UniqueName("moved")}@fintrack.test";
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, "/YourProfile");

        // Act
        await page.GetByLabel("New email").FillAsync(newEmail);
        await Button(page, "Change email").ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Status).Filter(new LocatorFilterOptions { HasText = newEmail }))
            .ToContainTextAsync("A link is on its way");
        string beforeOpening = await EmailOfAsync(user);

        CaughtMail mail = (await App.Mail.WaitForAsync(newEmail, AccountEmails.ChangeEmailSubject, CancellationToken))
            .Single();
        await page.GotoAsync(ChangeLink().Match(mail.Text).Groups["link"].Value);

        // Assert
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Level = 1 }))
            .ToHaveTextAsync("Email changed");
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        var stored = await context.Users.Where(x => x.Id == user.UserId)
            .Select(static x => new { x.Email, x.EmailConfirmed })
            .SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(beforeOpening).IsEqualTo($"{user.Username}@fintrack.test");
            await Assert.That(stored.Email).IsEqualTo(newEmail);
            await Assert.That(stored.EmailConfirmed).IsTrue();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    public async Task NoPassword_SaysTheProviderLooksAfterItAndTheApiRefuses()
    {
        // Arrange, as one made by signing in through the identity provider
        SignedInUser user = await RegisterUserAsync();
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            await context.Users.Where(x => x.Id == user.UserId)
                .ExecuteUpdateAsync(x => x.SetProperty(u => u.PasswordHash, (string?)null), CancellationToken);
        }

        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        IProfileService api = ApiFor(user).Get<IProfileService>();

        // Act
        await GoToAsync(page, "/YourProfile");
        ProfileChange? passwordChange = await api.ChangePasswordAsync(new ChangePassword
        {
            CurrentPassword = "anything", NewPassword = NewPassword, ConfirmNewPassword = NewPassword
        }, CancellationToken);
        ProfileChange? emailChange = await api.ChangeEmailAsync(new ChangeEmail { NewEmail = "other@fintrack.test" },
            CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assertions.Expect(page.Locator(".ft-page")).ToContainTextAsync("You sign in through your identity provider");
            await Assert.That(await page.GetByLabel("New email").CountAsync()).IsEqualTo(0);
            await Assert.That(passwordChange).IsNull();
            await Assert.That(emailChange).IsNull();
        }
    }

    /// <summary>
    /// Whether <paramref name="password"/> signs <paramref name="user"/> in, from a browser of its own.
    /// </summary>
    private async Task<bool> SignsInAsync(SignedInUser user, string password)
    {
        // Signing in redirects to the app, a wrong password shows the sign in page again
        using FinTrackClient client = CreateClient();
        HttpResponseMessage response = await client.LoginAsync(user.Username, password, CancellationToken);
        return response.StatusCode == System.Net.HttpStatusCode.Redirect;
    }

    private async Task<string> EmailOfAsync(SignedInUser user)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.Users.Where(x => x.Id == user.UserId).Select(static x => x.Email!)
            .SingleAsync(CancellationToken);
    }

    [GeneratedRegex(@"Use this email: (?<link>\S+)")]
    private static partial Regex ChangeLink();
}
