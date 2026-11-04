namespace FinanceTracker;

/// <summary>
/// Tells a user's open browsers that a background sync has started or finished for them, and admins' that a
/// background job has started or finished.
/// </summary>
public interface ISyncNotifier
{
    /// <summary>
    /// Tells <paramref name="userId"/>'s open browsers a sync of their banks has started, so they show it syncing
    /// whoever asked for it. Never throws, a missed message only leaves the page without the syncing note.
    /// </summary>
    Task NotifyStartedAsync(Guid userId);

    Task NotifyAsync(Guid userId);

    /// <summary>
    /// Tells admins' open browsers that <paramref name="function"/> has started or finished, so the automation page
    /// can show it. Never throws, a missed message only leaves the page behind.
    /// </summary>
    Task JobChangedAsync(string function);
}
