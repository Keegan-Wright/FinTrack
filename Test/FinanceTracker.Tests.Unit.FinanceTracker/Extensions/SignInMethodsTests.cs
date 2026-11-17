using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Components.Account;
using FinanceTracker.Extensions;
using Microsoft.Extensions.Configuration;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Extensions;

/// <summary>
/// Password sign in, single sign-on or both, never neither.
/// </summary>
public class SignInMethodsTests
{
    [Test]
    public async Task GetSignInMethods_ByDefault_IsPasswordOnly()
    {
        // Act
        SignInMethods methods = Configuration(passwordSignIn: null, singleSignOn: false).GetSignInMethods();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(methods.Password).IsTrue();
            await Assert.That(methods.SingleSignOn).IsFalse();
        }
    }

    [Test]
    public async Task GetSignInMethods_WithSingleSignOn_IsBoth()
    {
        // Act
        SignInMethods methods = Configuration(passwordSignIn: "true", singleSignOn: true).GetSignInMethods();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(methods.Password).IsTrue();
            await Assert.That(methods.SingleSignOn).IsTrue();
        }
    }

    [Test]
    public async Task GetSignInMethods_WithPasswordsOffAndSingleSignOn_IsSingleSignOnOnly()
    {
        // Act
        SignInMethods methods = Configuration(passwordSignIn: "false", singleSignOn: true).GetSignInMethods();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(methods.Password).IsFalse();
            await Assert.That(methods.SingleSignOn).IsTrue();
        }
    }

    [Test]
    public async Task GetSignInMethods_WithNeither_Throws()
    {
        // Arrange
        IConfiguration configuration = Configuration(passwordSignIn: "false", singleSignOn: false);

        // Act and assert
        await Assert.That(() => configuration.GetSignInMethods()).Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments(true, true, true)]
    [Arguments(true, false, false)]
    [Arguments(false, true, false)]
    public async Task CanUsePassword_OnlyWithPasswordSignInAndAPassword(bool passwordSignIn, bool hasPassword,
        bool expected)
    {
        // Arrange
        SignInMethods methods = new() { Password = passwordSignIn, SingleSignOn = true };

        // Act
        bool canUse = methods.CanUsePassword(hasPassword);

        // Assert
        await Assert.That(canUse).IsEqualTo(expected);
    }

    /// <summary>
    /// Settings with password sign in as given and single sign-on set up or not.
    /// </summary>
    /// <param name="passwordSignIn">The password sign in setting, null for not set.</param>
    /// <param name="singleSignOn">Whether single sign-on is set up.</param>
    private static IConfiguration Configuration(string? passwordSignIn, bool singleSignOn)
    {
        Dictionary<string, string?> settings = new() { [FinTrackEnvironment.PasswordSignIn] = passwordSignIn };
        if (singleSignOn)
        {
            settings[FinTrackEnvironment.OidcAuthority] = "https://auth.example.com";
            settings[FinTrackEnvironment.OidcClientId] = "fintrack";
            settings[FinTrackEnvironment.OidcClientSecret] = "secret";
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
