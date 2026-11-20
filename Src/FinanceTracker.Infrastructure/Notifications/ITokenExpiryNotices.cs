namespace FinanceTracker.Infrastructure.Notifications;

/// <summary>
/// Tells the current user when one of their API tokens is about to expire, or has, so whatever uses it doesn't stop
/// working unnoticed.
/// </summary>
public interface ITokenExpiryNotices
{
    /// <summary>
    /// Raises what's due for each of the user's tokens. A week out and once expired, each once per token, so it's
    /// safe to run on a schedule. Tokens that never expire, or were revoked, aren't told about.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    Task NotifyAsync(CancellationToken cancellationToken);
}
