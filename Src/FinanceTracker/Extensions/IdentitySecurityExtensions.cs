using FinanceTracker.Contracts.Auth;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure.Account.Authenticator;
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

    extension(IdentityBuilder identity)
    {
        /// <summary>
        /// Passwords of <see cref="PasswordRules"/> length with no character kind rules, hashed with
        /// <see cref="PasswordHashIterations"/>, and only the token providers the app uses. Identity's Email and Phone
        /// providers make HMAC-SHA1 codes, and its authenticator is SHA-1, so neither is registered.
        /// </summary>
        public IdentityBuilder AddFinTrackSecurity()
        {
            identity.Services.Configure<IdentityOptions>(static options =>
            {
                options.Password.RequiredLength = PasswordRules.MinimumLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            });
            identity.Services.Configure<PasswordHasherOptions>(static options =>
                options.IterationCount = PasswordHashIterations);

            // Email confirmation, email change and password reset links use the default, Data Protection, provider
            return identity
                .AddUserManager<FinanceTrackerUserManager>()
                .AddTokenProvider<DataProtectorTokenProvider<FinanceTrackerUser>>(TokenOptions.DefaultProvider)
                .AddTokenProvider<TotpAuthenticatorTokenProvider>(TokenOptions.DefaultAuthenticatorProvider);
        }
    }
}
