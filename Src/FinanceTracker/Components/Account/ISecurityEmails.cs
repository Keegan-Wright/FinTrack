using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure.Account.Email;

namespace FinanceTracker.Components.Account;

/// <summary>
/// Emails the user when how they sign in changes, so a change they didn't make doesn't go unnoticed. Only to a
/// confirmed email, and never failing the change. One that can't be sent is logged. There's no cancellation token on
/// purpose. The change is already saved when it's sent, so it goes even if the request that made it is cancelled.
/// The mail server's timeout stops it hanging.
/// </summary>
public interface ISecurityEmails
{
    /// <summary>
    /// Two-factor sign in was turned on for <paramref name="user"/>.
    /// </summary>
    /// <param name="user">The user.</param>
    Task TwoFactorOnAsync(FinanceTrackerUser user);

    /// <summary>
    /// Two-factor sign in was turned off for <paramref name="user"/>.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="cause">How it was turned off.</param>
    Task TwoFactorOffAsync(FinanceTrackerUser user, TwoFactorOffCause cause);

    /// <summary>
    /// New recovery codes were made for <paramref name="user"/>.
    /// </summary>
    /// <param name="user">The user.</param>
    Task RecoveryCodesMadeAsync(FinanceTrackerUser user);
}
