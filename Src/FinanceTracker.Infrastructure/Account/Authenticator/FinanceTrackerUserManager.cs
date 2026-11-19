using System.Security.Cryptography;
using FinanceTracker.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Account.Authenticator;

/// <summary>
/// Identity's <see cref="UserManager{TUser}"/> with authenticator keys sized for SHA-256. Identity's own are 160 bits,
/// sized for SHA-1.
/// </summary>
public sealed class FinanceTrackerUserManager : UserManager<FinanceTrackerUser>
{
    /// <inheritdoc/>
    public FinanceTrackerUserManager(IUserStore<FinanceTrackerUser> store, IOptions<IdentityOptions> optionsAccessor,
        IPasswordHasher<FinanceTrackerUser> passwordHasher,
        IEnumerable<IUserValidator<FinanceTrackerUser>> userValidators,
        IEnumerable<IPasswordValidator<FinanceTrackerUser>> passwordValidators, ILookupNormalizer keyNormalizer,
        IdentityErrorDescriber errors, IServiceProvider services, ILogger<UserManager<FinanceTrackerUser>> logger)
        : base(store, optionsAccessor, passwordHasher, userValidators, passwordValidators, keyNormalizer, errors,
            services, logger)
    {
    }

    /// <summary>
    /// A new random key of <see cref="AuthenticatorSettings.KeyBytes"/>, in base32.
    /// </summary>
    public override string GenerateNewAuthenticatorKey()
    {
        byte[] key = RandomNumberGenerator.GetBytes(AuthenticatorSettings.KeyBytes);
        try
        {
            return Base32.Encode(key);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
