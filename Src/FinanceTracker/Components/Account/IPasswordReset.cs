using FinanceTracker.Domain.Identity;

namespace FinanceTracker.Components.Account;

/// <summary>
/// Makes the one-time links that let a user choose a new password.
/// </summary>
public interface IPasswordReset
{
    /// <summary>
    /// When a link made now stops working, in UTC.
    /// </summary>
    DateTime ExpiresAt { get; }

    /// <summary>
    /// A link for <paramref name="user"/> to choose a new password with. Using it, or changing their password or
    /// email meanwhile, stops it working.
    /// </summary>
    Task<Uri> CreateLinkAsync(FinanceTrackerUser user);
}
