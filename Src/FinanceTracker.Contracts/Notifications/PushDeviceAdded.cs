namespace FinanceTracker.Contracts.Notifications;

/// <summary>
/// A browser added for push notifications.
/// </summary>
public sealed class PushDeviceAdded
{
    /// <summary>
    /// The added device's id, which the browser keeps to know it's on the list.
    /// </summary>
    public required Guid DeviceId { get; init; }

    /// <summary>
    /// The settings now.
    /// </summary>
    public required PushSettings Settings { get; init; }
}
