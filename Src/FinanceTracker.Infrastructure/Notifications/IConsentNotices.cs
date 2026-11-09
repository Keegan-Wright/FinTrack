namespace FinanceTracker.Infrastructure.Notifications;

/// <summary>
/// Tells the current user when a bank connection's consent is about to end, or has, so they renew it in time.
/// </summary>
public interface IConsentNotices
{
    /// <summary>
    /// Raises what's due for each of the user's connections. A week out, a day out and once ended. Each is raised once
    /// per consent, so it's safe to run on a schedule, and renewing starts afresh.
    /// </summary>
    Task NotifyAsync(CancellationToken cancellationToken);
}
