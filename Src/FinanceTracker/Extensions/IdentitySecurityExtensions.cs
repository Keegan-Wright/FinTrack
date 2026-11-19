using FinanceTracker.Contracts.Auth;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account.Authenticator;
using FinanceTracker.Infrastructure.Notifications;
using Microsoft.AspNetCore.Identity;

namespace FinanceTracker.Extensions;

/// <summary>
/// Replaces Identity's weaker defaults for password length, password hashing cost and the token providers.
/// </summary>
public static class IdentitySecurityExtensions
{
    /// <summary>
    /// PBKDF2-HMAC-SHA512 rounds for a password hash, OWASP's advice. Identity's default is 100,000, and a hash made
    /// with fewer is remade at its next sign in.
    /// </summary>
    public const int PasswordHashIterations = 210_000;

    /// <summary>
    /// The WebAuthn value that makes a passkey ask for a fingerprint, face or PIN, and that makes one be kept on the
    /// device so the browser can offer it without a username.
    /// </summary>
    private const string Required = "required";

    extension(IdentityBuilder identity)
    {
        /// <summary>
        /// Passwords of <see cref="PasswordRules"/> length with no character kind rules, hashed with
        /// <see cref="PasswordHashIterations"/>, and only the token providers the app uses. Identity's Email and Phone
        /// providers make HMAC-SHA1 codes, and its authenticator is SHA-1, so neither is registered. Passkeys belong
        /// to the host of <paramref name="publicAddress"/>, or the request's when it isn't set, and always ask for a
        /// fingerprint, face or PIN, so a passkey is two factors on its own.
        /// </summary>
        /// <param name="publicAddress">The app's address as people reach it.</param>
        public IdentityBuilder AddFinTrackSecurity(PublicAddress publicAddress)
        {
            ArgumentNullException.ThrowIfNull(publicAddress);
            identity.Services.Configure<IdentityOptions>(static options =>
            {
                options.Stores.SchemaVersion = FinanceTrackerContext.IdentitySchemaVersion;
                options.Password.RequiredLength = PasswordRules.MinimumLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            });
            identity.Services.Configure<PasswordHasherOptions>(static options =>
                options.IterationCount = PasswordHashIterations);
            identity.Services.Configure<IdentityPasskeyOptions>(options =>
            {
                options.ServerDomain = publicAddress.Url?.Host;
                options.UserVerificationRequirement = Required;
                options.ResidentKeyRequirement = Required;
            });

            // Email confirmation, email change and password reset links use the default, Data Protection, provider
            return identity
                .AddUserManager<FinanceTrackerUserManager>()
                .AddTokenProvider<DataProtectorTokenProvider<FinanceTrackerUser>>(TokenOptions.DefaultProvider)
                .AddTokenProvider<TotpAuthenticatorTokenProvider>(TokenOptions.DefaultAuthenticatorProvider);
        }
    }
}
