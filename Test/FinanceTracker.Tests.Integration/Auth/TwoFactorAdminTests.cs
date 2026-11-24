using FinanceTracker.Contracts.Profile;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Users;
using FinanceTracker.Infrastructure.Account.Email;
using FinanceTracker.Tests.Integration.Http;

namespace FinanceTracker.Tests.Integration.Auth;

/// <summary>
/// What happens around two-factor sign in. The user is emailed, an admin can turn it off for someone who's lost
/// their phone, and a password reset leaves it on.
/// </summary>
public class TwoFactorAdminTests : IntegrationTestBase
{
    [Test]
    public async Task TurnOn_EmailsTheUser()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();

        // Act
        await TurnOnAsync(user);

        // Assert
        IReadOnlyList<CaughtMail> mail =
            await App.Mail.WaitForAsync(EmailOf(user), AccountEmails.TwoFactorOnSubject, CancellationToken);
        await Assert.That(mail.Single().Text).Contains("also asks for a code from your authenticator app");
    }

    [Test]
    public async Task AdminTurnsItOff_TheyCanSignInWithJustTheirPasswordAndAreEmailed()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        SignedInUser user = await RegisterUserAsync();
        await TurnOnAsync(user);

        // Act
        bool turnedOff = await ApiFor(admin).Get<IUsersService>().TurnOffTwoFactorAsync(user.UserId, CancellationToken);

        // Assert
        using FinTrackClient client = CreateClient();
        HttpResponseMessage signIn =
            await client.LoginAsync(user.Username, SignedInUser.DefaultPassword, CancellationToken);
        IReadOnlyList<CaughtMail> mail =
            await App.Mail.WaitForAsync(EmailOf(user), AccountEmails.TwoFactorOffSubject, CancellationToken);
        UserSummary summary = await ApiFor(admin).Get<IUsersService>().GetUsersAsync(CancellationToken)
            .SingleAsync(x => x.Id == user.UserId, CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(turnedOff).IsTrue();
            await Assert.That(signIn.Headers.Location?.OriginalString).DoesNotContain("LoginWith2fa");
            await Assert.That(client.IsSignedIn).IsTrue();
            await Assert.That(summary.TwoFactorEnabled).IsFalse();
            await Assert.That(mail.Single().Text).Contains("Your FinTrack admin turned off two-factor sign in");
        }
    }

    [Test]
    public async Task AdminTurnsItOff_ForThemselves_IsRefused()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        await TurnOnAsync(admin);

        // Act
        bool turnedOff = await ApiFor(admin).Get<IUsersService>().TurnOffTwoFactorAsync(admin.UserId, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(turnedOff).IsFalse();
            await Assert.That((await ApiFor(admin).Get<IProfileService>().GetTwoFactorAsync(CancellationToken))!
                .Enabled).IsTrue();
        }
    }

    [Test]
    public async Task TurnOff_NotAnAdmin_IsForbidden()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        SignedInUser other = await RegisterUserAsync();

        // Act & Assert
        await Assert.That(() => ApiFor(user).Get<IUsersService>().TurnOffTwoFactorAsync(other.UserId, CancellationToken))
            .Throws<HttpRequestException>();
    }

    [Test]
    public async Task PasswordReset_LeavesTwoFactorOn()
    {
        // Arrange, a reset link alone mustn't get past the code
        SignedInUser admin = await RegisterAdminAsync();
        SignedInUser user = await RegisterUserAsync();
        await TurnOnAsync(user);
        ResetLink link = (await ApiFor(admin).Get<IUsersService>().CreateResetLinkAsync(user.UserId, CancellationToken))!;
        const string newPassword = "Reset-Placeholder-Password-7";

        // Act
        using FinTrackClient client = CreateClient();
        await client.SubmitFormAsync(new Uri(link.Url).PathAndQuery, new Dictionary<string, string>
        {
            ["Input.Password"] = newPassword,
            ["Input.ConfirmPassword"] = newPassword
        }, CancellationToken);
        HttpResponseMessage signIn = await client.LoginAsync(user.Username, newPassword, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(signIn.Headers.Location?.OriginalString).Contains("Account/LoginWith2fa");
            await Assert.That(client.IsSignedIn).IsFalse();
        }
    }

    /// <summary>
    /// Turns two-factor sign in on for <paramref name="user"/> as the profile page does.
    /// </summary>
    private async Task TurnOnAsync(SignedInUser user)
    {
        IProfileService profile = ApiFor(user).Get<IProfileService>();
        string key = (await profile.GetAuthenticatorSetupAsync(CancellationToken))!.SharedKey;
        TwoFactorChange? change = await profile.EnableAuthenticatorAsync(
            new EnableAuthenticator { Code = AuthenticatorApp.Code(key), Password = SignedInUser.DefaultPassword },
            CancellationToken);
        if (change is null or TwoFactorChangeRefused)
        {
            throw new InvalidOperationException($"Two-factor sign in couldn't be turned on for {user.Username}");
        }
    }

    /// <summary>
    /// The confirmed email <see cref="IntegrationTestBase.RegisterUserAsync"/> gave <paramref name="user"/>.
    /// </summary>
    private static string EmailOf(SignedInUser user) => $"{user.Username}@fintrack.test";
}
