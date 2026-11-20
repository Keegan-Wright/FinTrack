using System.Globalization;
using FinanceTracker.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace FinanceTracker.Components.Account;

/// <inheritdoc cref="ISingleSignOnConfirmation"/>
/// <remarks>Kept as an Identity user token, encrypted like the authenticator key.</remarks>
public sealed class SingleSignOnConfirmation : ISingleSignOnConfirmation
{
    /// <summary>
    /// How long a confirmation can be used for.
    /// </summary>
    public static readonly TimeSpan Lasts = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The user token's provider, FinTrack's own.
    /// </summary>
    private const string LoginProvider = "[FinTrack]";

    /// <summary>
    /// The user token's name.
    /// </summary>
    private const string TokenName = "SingleSignOnConfirmedAt";

    private readonly UserManager<FinanceTrackerUser> _userManager;
    private readonly TimeProvider _timeProvider;

    /// <param name="userManager">Keeps the confirmation with the user.</param>
    /// <param name="timeProvider">The clock it's timed by.</param>
    public SingleSignOnConfirmation(UserManager<FinanceTrackerUser> userManager, TimeProvider timeProvider)
    {
        _userManager = userManager;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task RecordAsync(FinanceTrackerUser user)
    {
        string now = _timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
        IdentityResult result = await _userManager.SetAuthenticationTokenAsync(user, LoginProvider, TokenName, now);
        if (!result.Succeeded)
        {
            throw new AccountUpdateFailedException(result);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsConfirmedAsync(FinanceTrackerUser user)
    {
        string? stored = await _userManager.GetAuthenticationTokenAsync(user, LoginProvider, TokenName);
        if (!DateTimeOffset.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                out DateTimeOffset confirmedAt))
        {
            return false;
        }

        return _timeProvider.GetUtcNow() - confirmedAt <= Lasts;
    }

    /// <inheritdoc/>
    public async Task<bool> UseAsync(FinanceTrackerUser user)
    {
        if (!await IsConfirmedAsync(user))
        {
            return false;
        }

        // Removing it saves the user, whose concurrency stamp lets only one of two racing uses through
        IdentityResult removed = await _userManager.RemoveAuthenticationTokenAsync(user, LoginProvider, TokenName);
        return removed.Succeeded;
    }
}
