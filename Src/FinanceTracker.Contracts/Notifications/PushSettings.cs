namespace FinanceTracker.Contracts.Notifications;

/// <summary>
/// The user's push notifications. The devices they get them on, and the key a browser needs to subscribe.
/// </summary>
public sealed class PushSettings
{
    /// <summary>
    /// This FinTrack's public VAPID key in base64url, the <c>applicationServerKey</c> a browser subscribes with.
    /// </summary>
    public required string PublicKey { get; init; }

    /// <summary>
    /// The devices they get push notifications on, oldest first.
    /// </summary>
    public required IReadOnlyList<PushDevice> Devices { get; init; }
}
