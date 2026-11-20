using FinanceTracker.Domain.Identity;

namespace FinanceTracker.Components.Account;

/// <summary>
/// That a user who signs in through the identity provider has just signed in there again, to confirm it's them
/// before something a password would otherwise be asked for, such as making an API token. Good for one use, within
/// <see cref="SingleSignOnConfirmation.Lasts"/>.
/// </summary>
public interface ISingleSignOnConfirmation
{
    /// <summary>
    /// Records that <paramref name="user"/> has just signed in at the provider again.
    /// </summary>
    /// <param name="user">The user.</param>
    Task RecordAsync(FinanceTrackerUser user);

    /// <summary>
    /// Whether <paramref name="user"/> has confirmed recently enough to use it.
    /// </summary>
    /// <param name="user">The user.</param>
    Task<bool> IsConfirmedAsync(FinanceTrackerUser user);

    /// <summary>
    /// Uses up <paramref name="user"/>'s confirmation, so it can't be used twice.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>Whether there was one recent enough to use.</returns>
    Task<bool> UseAsync(FinanceTrackerUser user);
}
