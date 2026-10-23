namespace FinanceTracker;

/// <summary>
/// Tells a user's open browsers that a background sync has finished for them.
/// </summary>
public interface ISyncNotifier
{
    Task NotifyAsync(Guid userId);
}
