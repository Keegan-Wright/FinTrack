using System.Text.Json;
using FinanceTracker.Contracts.Profile;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account.Email;
using FinanceTracker.Tests.Integration.Auth;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Adding a passkey from Your profile and signing in with it, with Chromium's virtual authenticator as the person's
/// device (<see cref="VirtualAuthenticator"/>).
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class PasskeyFlowTests : UiTestBase
{
    private const string PasskeyName = "Placeholder laptop";

    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task AddFromYourProfile_ThenSignsInWithItWithoutACode(ScreenSize screen)
    {
        // Arrange, with two-factor sign in on, which a passkey doesn't ask for
        SignedInUser user = await RegisterUserAsync();
        await TurnOnTwoFactorAsync(user);
        IPage page = await OpenAsync(screen, user);
        await VirtualAuthenticator.AddToAsync(page);

        // Act, add one
        await AddPasskeyAsync(page);
        await Assertions.Expect(page.Locator(".ft-passkeys__row")).ToHaveCountAsync(1);
        List<string> profileProblems = await AccessibilityProblemsAsync(page);
        List<string> profileTooWide = await WhatScrollsSidewaysAsync(page);

        // Then sign out and back in with it
        await Button(page, "Account").ClickAsync();
        await Button(page, "Sign out").ClickAsync();
        await page.WaitForURLAsync("**/Account/Login**");
        List<string> signInProblems = await AccessibilityProblemsAsync(page);
        await Button(page, "Sign in with a passkey").ClickAsync();

        // Assert
        await page.WaitForURLAsync(url => !url.Contains("/Account/", StringComparison.OrdinalIgnoreCase));
        IReadOnlyList<CaughtMail> mail =
            await App.Mail.WaitForAsync(EmailOf(user), AccountEmails.PasskeyAddedSubject, CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(page.Url).DoesNotContain("LoginWith2fa");
            await Assert.That(mail.Single().Text).Contains(PasskeyName);
            await Assert.That(profileProblems).IsEmpty();
            await Assert.That(profileTooWide).IsEmpty();
            await Assert.That(signInProblems).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    public async Task SignIn_FromTheUsernameField_SignsInAsThePageOpens()
    {
        // Arrange, the virtual authenticator picks the passkey the browser offers in the username field
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await VirtualAuthenticator.AddToAsync(page, offerInUsernameField: true);
        await AddPasskeyAsync(page);
        await Assertions.Expect(page.Locator(".ft-passkeys__row")).ToHaveCountAsync(1);

        // Act
        await page.Context.ClearCookiesAsync();
        await page.GotoAsync("/Account/Login");

        // Assert
        await page.WaitForURLAsync(url => !url.Contains("/Account/", StringComparison.OrdinalIgnoreCase));
        await Assert.That(BrowserErrors).IsEmpty();
    }

    [Test]
    public async Task Added_IsStoredWithItsNameKeyAndRecordEncrypted()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await VirtualAuthenticator.AddToAsync(page);

        // Act
        await AddPasskeyAsync(page);
        await Assertions.Expect(page.Locator(".ft-passkeys__row")).ToHaveCountAsync(1);

        // Assert, the column as it's stored
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        string stored = await context.Database
            .SqlQuery<string>($"""
                SELECT "Data"::text AS "Value" FROM "AspNetUserPasskeys" WHERE "UserId" = {user.UserId}
                """)
            .SingleAsync(CancellationToken);
        using JsonDocument data = JsonDocument.Parse(stored);
        string[] encrypted = ["Name", "PublicKey", "AttestationObject", "ClientDataJson"];
        using (Assert.Multiple())
        {
            await Assert.That(stored).DoesNotContain(PasskeyName);
            foreach (string field in encrypted)
            {
                // Stored as {keyId}:{ciphertext}, as every encrypted column is
                await Assert.That(data.RootElement.GetProperty(field).GetString()).StartsWith("1:");
            }
        }
    }

    [Test]
    public async Task SignIn_LockedByAnAdmin_IsTurnedAway()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await VirtualAuthenticator.AddToAsync(page);
        await AddPasskeyAsync(page);
        await Assertions.Expect(page.Locator(".ft-passkeys__row")).ToHaveCountAsync(1);
        await ApiFor(admin).Get<IUsersService>().LockAsync(user.UserId, CancellationToken);

        // Act
        await page.Context.ClearCookiesAsync();
        await page.GotoAsync("/Account/Login");
        await Button(page, "Sign in with a passkey").ClickAsync();

        // Assert
        await page.WaitForURLAsync("**/Account/Lockout**");
        await Assert.That(page.Url).Contains("byAdmin=true");
    }

    [Test]
    public async Task SignIn_AfterAnAdminRemovedThem_DoesNotWork()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await VirtualAuthenticator.AddToAsync(page);
        await AddPasskeyAsync(page);
        await Assertions.Expect(page.Locator(".ft-passkeys__row")).ToHaveCountAsync(1);
        bool removed = await ApiFor(admin).Get<IUsersService>().RemovePasskeysAsync(user.UserId, CancellationToken);

        // Act
        await page.Context.ClearCookiesAsync();
        await page.GotoAsync("/Account/Login");
        await Button(page, "Sign in with a passkey").ClickAsync();

        // Assert
        await Assertions.Expect(page.GetByText("That passkey didn't work")).ToBeVisibleAsync();
        IReadOnlyList<CaughtMail> mail =
            await App.Mail.WaitForAsync(EmailOf(user), AccountEmails.PasskeyRemovedSubject, CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(removed).IsTrue();
            await Assert.That(new Uri(page.Url).AbsolutePath).IsEqualTo("/Account/Login");
            await Assert.That(mail.Single().Text).Contains("Your FinTrack admin removed all your passkeys");
        }
    }

    /// <summary>
    /// Adds a passkey from the Security tab of Your profile, as the person would.
    /// </summary>
    private static async Task AddPasskeyAsync(IPage page)
    {
        await GoToAsync(page, "/YourProfile");
        await page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = "Security" }).ClickAsync();
        await Button(page, "Add a passkey").ClickAsync();
        ILocator dialog = Dialog(page);
        await dialog.GetByLabel("Name").FillAsync(PasskeyName);
        await dialog.GetByLabel("Your password").FillAsync(SignedInUser.DefaultPassword);
        await Button(page, "OK").ClickAsync();
    }

    /// <summary>
    /// Turns two-factor sign in on for <paramref name="user"/> through the API.
    /// </summary>
    private async Task TurnOnTwoFactorAsync(SignedInUser user)
    {
        IProfileService profile = ApiFor(user).Get<IProfileService>();
        string key = (await profile.GetAuthenticatorSetupAsync(CancellationToken))!.SharedKey;
        await profile.EnableAuthenticatorAsync(new EnableAuthenticator
        {
            Code = AuthenticatorApp.Code(key), Password = SignedInUser.DefaultPassword
        }, CancellationToken);
    }

    /// <summary>
    /// The confirmed email <see cref="IntegrationTestBase.RegisterUserAsync"/> gave <paramref name="user"/>.
    /// </summary>
    private static string EmailOf(SignedInUser user) => $"{user.Username}@fintrack.test";
}
