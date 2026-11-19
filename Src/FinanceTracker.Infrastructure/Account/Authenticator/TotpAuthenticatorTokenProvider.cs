using System.Globalization;
using System.Security.Cryptography;
using FinanceTracker.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace FinanceTracker.Infrastructure.Account.Authenticator;

/// <summary>
/// Checks authenticator app codes in place of Identity's SHA-1 provider. HMAC-SHA256 with
/// <see cref="AuthenticatorSettings"/>, and each code taken once only, as RFC 6238 section 5.2 asks. Registered under
/// Identity's authenticator provider name so nothing can reach the SHA-1 one.
/// </summary>
public sealed class TotpAuthenticatorTokenProvider : IUserTwoFactorTokenProvider<FinanceTrackerUser>
{
    /// <summary>
    /// The user token the last step used is kept under, beside the key and recovery codes and encrypted like them.
    /// </summary>
    private const string LoginProvider = "[AspNetUserStore]";

    /// <summary>
    /// The name of the token holding the last step used.
    /// </summary>
    private const string LastStepName = "AuthenticatorLastStep";

    private readonly TimeProvider _timeProvider;

    /// <param name="timeProvider">The clock codes are checked against.</param>
    public TotpAuthenticatorTokenProvider(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Nothing, codes come from the user's app and the server never makes one.
    /// </summary>
    public Task<string> GenerateAsync(string purpose, UserManager<FinanceTrackerUser> manager,
        FinanceTrackerUser user) => Task.FromResult(string.Empty);

    /// <summary>
    /// Whether <paramref name="token"/> is the user's code for now, give or take the allowed drift, and newer than
    /// the last one used. A code that's taken is recorded so it can't be used again; when two requests race with the
    /// same code, the user's concurrency stamp lets only one save.
    /// </summary>
    public async Task<bool> ValidateAsync(string purpose, string token, UserManager<FinanceTrackerUser> manager,
        FinanceTrackerUser user)
    {
        ArgumentNullException.ThrowIfNull(manager);
        string? key = await manager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        long currentStep = Totp.StepAt(_timeProvider.GetUtcNow());
        long? matchedStep = MatchingStep(key, token, currentStep);
        if (matchedStep is null)
        {
            return false;
        }

        long? lastStep = await LastStepAsync(manager, user);
        if (lastStep is not null && matchedStep <= lastStep)
        {
            return false;
        }

        IdentityResult saved = await manager.SetAuthenticationTokenAsync(user, LoginProvider, LastStepName,
            matchedStep.Value.ToString(CultureInfo.InvariantCulture));
        return saved.Succeeded;
    }

    /// <summary>
    /// Whether the user has a key to make codes from.
    /// </summary>
    public async Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<FinanceTrackerUser> manager,
        FinanceTrackerUser user)
    {
        ArgumentNullException.ThrowIfNull(manager);
        return !string.IsNullOrEmpty(await manager.GetAuthenticatorKeyAsync(user));
    }

    /// <summary>
    /// The step <paramref name="code"/> matches for <paramref name="key"/>, with the key's bytes cleared after.
    /// </summary>
    /// <param name="key">The stored base32 key.</param>
    /// <param name="code">The code typed.</param>
    /// <param name="currentStep">The step now.</param>
    private static long? MatchingStep(string key, string code, long currentStep)
    {
        byte[] keyBytes = Base32.Decode(key);
        try
        {
            return Totp.MatchingStep(keyBytes, code, currentStep);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyBytes);
        }
    }

    /// <summary>
    /// The step of the last code used, or null before the first.
    /// </summary>
    private static async Task<long?> LastStepAsync(UserManager<FinanceTrackerUser> manager, FinanceTrackerUser user)
    {
        string? stored = await manager.GetAuthenticationTokenAsync(user, LoginProvider, LastStepName);
        if (long.TryParse(stored, NumberStyles.None, CultureInfo.InvariantCulture, out long step))
        {
            return step;
        }

        return null;
    }
}
