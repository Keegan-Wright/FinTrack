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
    /// <param name="userId">The user whose sync started.</param>
    Task NotifyStartedAsync(Guid userId);

    /// <summary>
    /// Tells <paramref name="userId"/>'s open browsers a sync of their banks has finished, so pages showing synced
    /// data catch up. Never throws, a missed message only leaves the page's sync time stale.
    /// </summary>
    /// <param name="userId">The user whose sync finished.</param>
    Task NotifyAsync(Guid userId);

    /// <summary>
    /// Tells admins' open browsers that <paramref name="function"/> has started or finished, so the automation page
    /// can show it. Never throws, a missed message only leaves the page behind.
    /// </summary>
    /// <param name="function">The job's name.</param>
    Task JobChangedAsync(string function);
}
