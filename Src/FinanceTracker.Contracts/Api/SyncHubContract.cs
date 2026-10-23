namespace FinanceTracker.Contracts.Api;

/// <summary>
/// The SignalR hub the server tells a user's browsers about finished syncs through, see
/// <see cref="Services.ISyncNotifications"/>.
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
}
