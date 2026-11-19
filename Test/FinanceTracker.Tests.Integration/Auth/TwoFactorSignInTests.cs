using FinanceTracker.Contracts.Profile;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account.Authenticator;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Auth;

/// <summary>
/// Two-factor sign in with an authenticator app, turned on through the profile API and used through the sign in
/// pages, the codes made by <see cref="AuthenticatorApp"/> as an app would.
/// </summary>
public class TwoFactorSignInTests : IntegrationTestBase
{
    [Test]
    public async Task TurnOn_WithTheRightCode_GivesTenRecoveryCodesAndHidesTheKey()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IProfileService profile = ApiFor(user).Get<IProfileService>();

        // Act
        IReadOnlyList<string> codes = await TurnOnAsync(profile);

        // Assert
        TwoFactorStatus? status = await profile.GetTwoFactorAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(codes.Count).IsEqualTo(10);
            await Assert.That(status!.Enabled).IsTrue();
            await Assert.That(status.RecoveryCodesLeft).IsEqualTo(10);
            await Assert.That(await profile.GetAuthenticatorSetupAsync(CancellationToken)).IsNull();
        }
    }

    [Test]
    public async Task TurnOn_StoresTheKeyAndRecoveryCodesEncrypted()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IProfileService profile = ApiFor(user).Get<IProfileService>();
        string key = (await profile.GetAuthenticatorSetupAsync(CancellationToken))!.SharedKey;

        // Act
        IReadOnlyList<string> codes = await TurnOnAsync(profile, key);

        // Assert, the columns as they're stored
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        List<string> stored = await context.Database
            .SqlQuery<string>($"""
                SELECT "Value" AS "Value" FROM "AspNetUserTokens" WHERE "UserId" = {user.UserId}
                """)
            .ToListAsync(CancellationToken);
        string plainKey = key.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        using (Assert.Multiple())
        {
            // The key, the recovery codes and the step of the last code used
            await Assert.That(stored.Count).IsEqualTo(3);
            await Assert.That(stored.Exists(x => x.Contains(plainKey, StringComparison.OrdinalIgnoreCase))).IsFalse();
            await Assert.That(stored.Exists(x => x.Contains(codes[0], StringComparison.Ordinal))).IsFalse();
        }
    }

    [Test]
    public async Task TurnOn_WrongPasswordOrCode_IsRefused()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IProfileService profile = ApiFor(user).Get<IProfileService>();
        AuthenticatorSetup setup = (await profile.GetAuthenticatorSetupAsync(CancellationToken))!;

        // Act
        TwoFactorChange? wrongPassword = await profile.EnableAuthenticatorAsync(
            new EnableAuthenticator { Code = AuthenticatorApp.Code(setup.SharedKey), Password = "not-the-password" },
            CancellationToken);
        TwoFactorChange? wrongCode = await profile.EnableAuthenticatorAsync(
            new EnableAuthenticator
            {
                Code = AuthenticatorApp.WrongCode(setup.SharedKey), Password = SignedInUser.DefaultPassword
            },
            CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(wrongPassword!.Errors).IsEquivalentTo(["Your password isn't right"]);
            await Assert.That(wrongCode!.Succeeded).IsFalse();
            await Assert.That((await profile.GetTwoFactorAsync(CancellationToken))!.Enabled).IsFalse();
        }
    }

    [Test]
    public async Task SignIn_WithItOn_AsksForACodeThenSignsIn()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IProfileService profile = ApiFor(user).Get<IProfileService>();
        string key = (await profile.GetAuthenticatorSetupAsync(CancellationToken))!.SharedKey;
        await TurnOnAsync(profile, key);
        using FinTrackClient client = CreateClient();

        // Act
        HttpResponseMessage password =
            await client.LoginAsync(user.Username, SignedInUser.DefaultPassword, CancellationToken);
        bool signedInAfterPassword = client.IsSignedIn;
        HttpResponseMessage code = await client.SubmitFormAsync(password.Headers.Location!.OriginalString,
            new Dictionary<string, string> { ["Input.Code"] = AuthenticatorApp.Code(key, stepsFromNow: 1) },
            CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(password.Headers.Location!.OriginalString).Contains("Account/LoginWith2fa");
            await Assert.That(signedInAfterPassword).IsFalse();
            await Assert.That(code.StatusCode).IsEqualTo(System.Net.HttpStatusCode.Redirect);
            await Assert.That(client.IsSignedIn).IsTrue();
        }
    }

    [Test]
    public async Task SignIn_WithTheCodeUsedToTurnItOn_IsRefused()
    {
        // Arrange, turned on with the code for now
        SignedInUser user = await RegisterUserAsync();
        IProfileService profile = ApiFor(user).Get<IProfileService>();
        string key = (await profile.GetAuthenticatorSetupAsync(CancellationToken))!.SharedKey;
        string code = AuthenticatorApp.Code(key);
        await profile.EnableAuthenticatorAsync(
            new EnableAuthenticator { Code = code, Password = SignedInUser.DefaultPassword }, CancellationToken);

        // Act, the same code again, as someone who watched it typed would
        bool signedIn = await SignInWithCodeAsync(user, code);

        // Assert
        await Assert.That(signedIn).IsFalse();
    }

    [Test]
    public async Task SignIn_WithACodeOutsideTheAllowedDrift_IsRefused()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IProfileService profile = ApiFor(user).Get<IProfileService>();
        string key = (await profile.GetAuthenticatorSetupAsync(CancellationToken))!.SharedKey;
        await TurnOnAsync(profile, key);

        // Act
        bool signedIn = await SignInWithCodeAsync(user,
            AuthenticatorApp.Code(key, stepsFromNow: AuthenticatorSettings.AllowedDriftSteps + 1));

        // Assert
        await Assert.That(signedIn).IsFalse();
    }

    [Test]
    public async Task Setup_GivesA256BitKeyForSha256()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();

        // Act
        AuthenticatorSetup setup =
            (await ApiFor(user).Get<IProfileService>().GetAuthenticatorSetupAsync(CancellationToken))!;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Base32.Decode(setup.SharedKey).Length).IsEqualTo(32);
            await Assert.That(setup.QrCodeSvg).StartsWith("<svg");
        }
    }

    [Test]
    public async Task SignIn_WithARecoveryCode_WorksOnce()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IReadOnlyList<string> codes = await TurnOnAsync(ApiFor(user).Get<IProfileService>());

        // Act
        bool first = await SignInWithRecoveryCodeAsync(user, codes[0]);
        bool again = await SignInWithRecoveryCodeAsync(user, codes[0]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first).IsTrue();
            await Assert.That(again).IsFalse();
        }
    }

    [Test]
    public async Task SignIn_WrongCodes_LockTheAccount()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IProfileService profile = ApiFor(user).Get<IProfileService>();
        string key = (await profile.GetAuthenticatorSetupAsync(CancellationToken))!.SharedKey;
        await TurnOnAsync(profile, key);
        using FinTrackClient client = CreateClient();
        HttpResponseMessage password =
            await client.LoginAsync(user.Username, SignedInUser.DefaultPassword, CancellationToken);
        string codePage = password.Headers.Location!.OriginalString;

        // Act, five wrong codes, as many as wrong passwords allowed
        HttpResponseMessage last = password;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            last = await client.SubmitFormAsync(codePage,
                new Dictionary<string, string> { ["Input.Code"] = AuthenticatorApp.WrongCode(key) }, CancellationToken);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(last.Headers.Location?.OriginalString).Contains("Account/Lockout");
            await Assert.That(client.IsSignedIn).IsFalse();
        }
    }

    [Test]
    public async Task TurnOff_WithThePassword_StopsAskingForACode()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IProfileService profile = ApiFor(user).Get<IProfileService>();
        await TurnOnAsync(profile);

        // Act
        TwoFactorChange? change = await profile.TurnOffTwoFactorAsync(
            new ConfirmPassword { Password = SignedInUser.DefaultPassword }, CancellationToken);

        // Assert
        using FinTrackClient client = CreateClient();
        HttpResponseMessage signIn =
            await client.LoginAsync(user.Username, SignedInUser.DefaultPassword, CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(change!.Succeeded).IsTrue();
            await Assert.That(signIn.Headers.Location?.OriginalString).DoesNotContain("LoginWith2fa");
            await Assert.That(client.IsSignedIn).IsTrue();
        }
    }

    [Test]
    public async Task TwoFactor_ForAnAccountWithoutAPassword_IsNotOffered()
    {
        // Arrange, as one made by signing in through the identity provider
        SignedInUser user = await RegisterUserAsync();
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            await context.Users.Where(x => x.Id == user.UserId)
                .ExecuteUpdateAsync(x => x.SetProperty(u => u.PasswordHash, (string?)null), CancellationToken);
        }

        // Act
        TwoFactorStatus? status = await ApiFor(user).Get<IProfileService>().GetTwoFactorAsync(CancellationToken);

        // Assert
        await Assert.That(status).IsNull();
    }

    /// <summary>
    /// Turns two-factor sign in on as the profile page does.
    /// </summary>
    /// <param name="key">The key already fetched, fetched here when null.</param>
    /// <returns>The recovery codes.</returns>
    private async Task<IReadOnlyList<string>> TurnOnAsync(IProfileService profile, string? key = null)
    {
        key ??= (await profile.GetAuthenticatorSetupAsync(CancellationToken))!.SharedKey;
        TwoFactorChange? change = await profile.EnableAuthenticatorAsync(
            new EnableAuthenticator { Code = AuthenticatorApp.Code(key), Password = SignedInUser.DefaultPassword },
            CancellationToken);
        return change!.RecoveryCodes;
    }

    /// <summary>
    /// Signs in with the password then <paramref name="code"/>.
    /// </summary>
    /// <returns>Whether it signed them in.</returns>
    private async Task<bool> SignInWithCodeAsync(SignedInUser user, string code)
    {
        using FinTrackClient client = CreateClient();
        HttpResponseMessage password =
            await client.LoginAsync(user.Username, SignedInUser.DefaultPassword, CancellationToken);
        await client.SubmitFormAsync(password.Headers.Location!.OriginalString,
            new Dictionary<string, string> { ["Input.Code"] = code }, CancellationToken);
        return client.IsSignedIn;
    }

    /// <summary>
    /// Signs in with the password then a recovery code.
    /// </summary>
    /// <returns>Whether it signed them in.</returns>
    private async Task<bool> SignInWithRecoveryCodeAsync(SignedInUser user, string recoveryCode)
    {
        using FinTrackClient client = CreateClient();
        await client.LoginAsync(user.Username, SignedInUser.DefaultPassword, CancellationToken);
        await client.SubmitFormAsync("/Account/LoginWithRecoveryCode",
            new Dictionary<string, string> { ["Input.RecoveryCode"] = recoveryCode }, CancellationToken);
        return client.IsSignedIn;
    }
}
