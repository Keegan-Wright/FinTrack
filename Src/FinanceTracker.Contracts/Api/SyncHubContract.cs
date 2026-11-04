namespace FinanceTracker.Contracts.Api;

/// <summary>
/// The SignalR hub the server tells a user's browsers about syncs starting and finishing through, and admins' browsers about
/// background jobs, see <see cref="Services.ISyncNotifications"/>.
/// </summary>
public static class SyncHubContract
{
    /// <summary>
    /// Where the hub is, relative to the app's base address. Needs a signed in user.
    /// </summary>
    public const string Path = "hubs/sync";

    /// <summary>
    /// The method the hub calls, with no arguments, when a sync of the connected user's banks starts, on its
    /// schedule or asked for anywhere.
    /// </summary>
    public const string SyncStarted = "SyncStarted";

    /// <summary>
    /// The method the hub calls, with no arguments, when a sync for the connected user finishes.
    /// </summary>
    public const string SyncComplete = "SyncComplete";

    /// <summary>
    /// The method the hub calls on admins' browsers, with the job's function, when a background job starts or
    /// finishes.
    /// </summary>
    public const string JobChanged = "JobChanged";
}
