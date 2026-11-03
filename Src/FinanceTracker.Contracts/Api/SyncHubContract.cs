namespace FinanceTracker.Contracts.Api;

/// <summary>
/// The SignalR hub the server tells a user's browsers about finished syncs through, and admins' browsers about
/// background jobs, see <see cref="Services.ISyncNotifications"/>.
/// </summary>
public static class SyncHubContract
{
    /// <summary>
    /// Where the hub is, relative to the app's base address. Needs a signed in user.
    /// </summary>
    public const string Path = "hubs/sync";

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
