using System.Text.RegularExpressions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account.Email;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Confirming an email by the link FinTrack sends it, on registering or from the banner asking for it. Notifications
/// and password resets are only emailed to a confirmed one.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public partial class ConfirmEmailTests : UiTestBase
{
    [Test]
    public async Task Register_EmailsALinkThatConfirmsTheEmail()
    {
        // Arrange, registering sends the link
        SignedInUser user = await RegisterUserAsync(confirmEmail: false);
        string email = $"{user.Username}@fintrack.test";
        string link = await ConfirmLinkAsync(email);
        IPage page = await OpenAsync(ScreenSize.Phone);

        // Act, opened from the email, signed in or not
        await page.GotoAsync(link);

        // Assert
        using (Assert.Multiple())
        {
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Level = 1 }))
                .ToHaveTextAsync("Email confirmed");
            await Assert.That(await IsConfirmedAsync(user)).IsTrue();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task Banner_SendClicked_EmailsAnotherLink(ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync(confirmEmail: false);
        string email = $"{user.Username}@fintrack.test";
        await ConfirmLinkAsync(email);
        IPage page = await OpenAsync(screen, user);
        await GoToAsync(page, "/");
        ILocator banner = page.Locator(".ft-confirm-banner");
        await Assertions.Expect(banner).ToContainTextAsync("Confirm your email");

        // Act
        await banner.GetByText("Send confirmation link").ClickAsync();

        // Assert
        await Assertions.Expect(banner).ToContainTextAsync($"A link is on its way to {email}.");
        using (Assert.Multiple())
        {
            await Assert.That(await LinksSentToAsync(email)).Count().IsEqualTo(2);
            await Assert.That(await FitsOnScreenAsync(page, banner)).IsTrue();
            await Assert.That(await AccessibilityProblemsAsync(page)).IsEmpty();
        }
    }

    [Test]
    public async Task Confirmed_ShowsNoBanner()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);

        // Act
        await GoToAsync(page, "/");

        // Assert
        await Assert.That(await page.Locator(".ft-confirm-banner").CountAsync()).IsEqualTo(0);
    }

    [Test]
    public async Task ConfirmEmail_LinkChanged_SaysItIsntValid()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync(confirmEmail: false);
        IPage page = await OpenAsync(ScreenSize.Phone);

        // Act
        await page.GotoAsync($"/Account/ConfirmEmail?userId={user.UserId}&code=bm90LWEtdG9rZW4");

        // Assert
        using (Assert.Multiple())
        {
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Level = 1 }))
                .ToHaveTextAsync("That link isn't valid");
            await Assert.That(await IsConfirmedAsync(user)).IsFalse();
        }
    }

    /// <summary>
    /// The link in the newest confirmation email to <paramref name="email"/>, once one has arrived.
    /// </summary>
    private async Task<string> ConfirmLinkAsync(string email) => (await LinksSentToAsync(email))[0];

    private async Task<List<string>> LinksSentToAsync(string email) =>
        (await App.Mail.WaitForAsync(email, AccountEmails.ConfirmEmailSubject, CancellationToken))
        .Select(static x => ConfirmLink().Match(x.Text).Groups["link"].Value)
        .ToList();

    private async Task<bool> IsConfirmedAsync(SignedInUser user)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.Users.Where(x => x.Id == user.UserId).Select(static x => x.EmailConfirmed)
            .SingleAsync(CancellationToken);
    }

    [GeneratedRegex(@"Confirm your email: (?<link>\S+)")]
    private static partial Regex ConfirmLink();
}
