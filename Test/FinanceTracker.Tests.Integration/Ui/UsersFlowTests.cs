using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Users;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// An admin's users page, and the reset link it makes for someone who can't be emailed one.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class UsersFlowTests : UiTestBase
{
    private const string NewPassword = "Handed-Over-Password-5!";

    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task Page_AsAnAdmin_ListsHowEachSignsInAndFitsTheScreen(ScreenSize screen)
    {
        // Arrange
        SignedInUser local = await RegisterUserAsync();
        SignedInUser provider = await RegisterUserAsync();
        await RemovePasswordAsync(provider);
        SignedInUser admin = await RegisterAdminAsync();
        IPage page = await OpenAsync(screen, admin);

        // Act
        await GoToAsync(page, "/Users");

        // Assert
        ILocator localRow = Row(page, local);
        ILocator providerRow = Row(page, provider);
        await Assertions.Expect(localRow).ToContainTextAsync("Signs in with a password");
        using (Assert.Multiple())
        {
            await Assertions.Expect(localRow).ToContainTextAsync($"{local.Username}@fintrack.test · Confirmed");
            await Assertions.Expect(ResetButton(localRow)).ToBeVisibleAsync();
            await Assertions.Expect(providerRow).ToContainTextAsync("Signs in through the identity provider");
            await Assert.That(await ResetButton(providerRow).CountAsync()).IsEqualTo(0);
            await Assertions.Expect(Row(page, admin)).ToContainTextAsync("Admin");
            await Assert.That(await WhatScrollsSidewaysAsync(page)).IsEmpty();
            await Assert.That(await AccessibilityProblemsAsync(page)).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    public async Task ResetLink_HandedToTheUser_LetsThemChooseANewPassword()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        SignedInUser admin = await RegisterAdminAsync();
        IPage adminPage = await OpenAsync(ScreenSize.Desktop, admin);
        await GoToAsync(adminPage, "/Users");

        // Act, the admin makes the link
        await ResetButton(Row(adminPage, user)).ClickAsync();
        await Assertions.Expect(Dialog(adminPage)).ToContainTextAsync($"Send this to {user.Username} yourself");
        string link = await Dialog(adminPage).GetByLabel("Reset link").InputValueAsync();

        // And the user, signed out in their own browser, opens it
        IPage userPage = await OpenAsync(ScreenSize.Phone);
        await userPage.GotoAsync(link);
        await userPage.GetByLabel("New password", new PageGetByLabelOptions { Exact = true }).FillAsync(NewPassword);
        await userPage.GetByLabel("Confirm new password").FillAsync(NewPassword);
        await Button(userPage, "Reset password").ClickAsync();

        // Assert
        await Assertions.Expect(userPage.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Level = 1 }))
            .ToHaveTextAsync("Password reset");
        using FinTrackClient client = CreateClient();
        HttpResponseMessage signIn = await client.LoginAsync(user.Username, NewPassword, CancellationToken);
        await Assert.That(signIn.StatusCode).IsEqualTo(System.Net.HttpStatusCode.Redirect);
    }

    [Test]
    public async Task ResetLink_NoPasswordOrNoSuchUser_IsRefused()
    {
        // Arrange
        SignedInUser provider = await RegisterUserAsync();
        await RemovePasswordAsync(provider);
        IUsersService users = ApiFor(await RegisterAdminAsync()).Get<IUsersService>();

        // Act
        ResetLink? forProvider = await users.CreateResetLinkAsync(provider.UserId, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(forProvider).IsNull();
            await Assert.That(() => users.CreateResetLinkAsync(Guid.NewGuid(), CancellationToken))
                .Throws<InvalidOperationException>();
        }
    }

    [Test]
    public async Task Lock_ByAnAdmin_TurnsTheirSignInAwayUntilUnlocked()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        SignedInUser admin = await RegisterAdminAsync();
        IPage adminPage = await OpenAsync(ScreenSize.Desktop, admin);
        await GoToAsync(adminPage, "/Users");

        // Act, the admin locks them
        await Button(adminPage, $"Lock {user.Username}").ClickAsync();
        await Dialog(adminPage).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Lock", Exact = true })
            .ClickAsync();
        await Assertions.Expect(Row(adminPage, user)).ToContainTextAsync("Locked");

        // Assert, they're turned away and told why
        IPage userPage = await OpenAsync(ScreenSize.Phone);
        await userPage.GotoAsync("/Account/Login");
        await userPage.GetByLabel("Username").FillAsync(user.Username);
        await userPage.GetByLabel("Password", new PageGetByLabelOptions { Exact = true })
            .FillAsync(SignedInUser.DefaultPassword);
        await Button(userPage, "Sign in").ClickAsync();
        await Assertions.Expect(userPage.GetByText("An admin has locked this account")).ToBeVisibleAsync();

        // And once unlocked they're let in
        await Button(adminPage, $"Unlock {user.Username}").ClickAsync();
        await Assertions.Expect(Row(adminPage, user)).Not.ToContainTextAsync("Locked");
        using FinTrackClient client = CreateClient();
        HttpResponseMessage signIn = await client.LoginAsync(user.Username, SignedInUser.DefaultPassword, CancellationToken);
        await Assert.That(signIn.Headers.Location?.OriginalString).DoesNotContain("Lockout");
    }

    [Test]
    public async Task Lock_Locked_SignsOutTheirSessions()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IUsersService users = ApiFor(await RegisterAdminAsync()).Get<IUsersService>();
        string? stampBefore = await SecurityStampAsync(user);

        // Act
        bool locked = await users.LockAsync(user.UserId, CancellationToken);

        // Assert, a new stamp turns their cookies away the next time they're checked
        using (Assert.Multiple())
        {
            await Assert.That(locked).IsTrue();
            await Assert.That(await SecurityStampAsync(user)).IsNotEqualTo(stampBefore);
        }
    }

    [Test]
    public async Task Lock_YourselfOrNoSuchUser_IsRefused()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        IUsersService users = ApiFor(admin).Get<IUsersService>();

        // Act
        bool lockedYourself = await users.LockAsync(admin.UserId, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(lockedYourself).IsFalse();
            await Assert.That(() => users.LockAsync(Guid.NewGuid(), CancellationToken))
                .Throws<InvalidOperationException>();
            await Assert.That(() => users.UnlockAsync(Guid.NewGuid(), CancellationToken))
                .Throws<InvalidOperationException>();
        }
    }

    private static ILocator Row(IPage page, SignedInUser user) =>
        page.Locator(".ft-users__row").Filter(new LocatorFilterOptions { HasText = user.Username });

    private static ILocator ResetButton(ILocator row) =>
        row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Make a reset link for" });

    private async Task<string?> SecurityStampAsync(SignedInUser user)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.Users.Where(x => x.Id == user.UserId).Select(static x => x.SecurityStamp)
            .SingleAsync(CancellationToken);
    }

    /// <summary>
    /// As one made by signing in through the identity provider, whose password is the provider's.
    /// </summary>
    private async Task RemovePasswordAsync(SignedInUser user)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        await context.Users.Where(x => x.Id == user.UserId)
            .ExecuteUpdateAsync(x => x.SetProperty(u => u.PasswordHash, (string?)null), CancellationToken);
    }
}
