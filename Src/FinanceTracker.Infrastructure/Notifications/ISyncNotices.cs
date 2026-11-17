namespace FinanceTracker.Infrastructure.Notifications;

/// <summary>
/// Tells the current user about something a sync may have changed, e.g. a bill going unpaid or an account running
/// low. Each is run for each owner at the end of a sync, see <c>OpenBankingSyncJob</c>, and raises its notifications
/// through <see cref="INotifier"/>, which keeps each event to one.
/// </summary>
public interface ISyncNotices
{
    /// <summary>
    /// Raises the notifications for what the sync changed.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    Task NotifyAsync(CancellationToken cancellationToken);
}
