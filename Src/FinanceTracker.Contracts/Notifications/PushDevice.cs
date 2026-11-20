namespace FinanceTracker.Contracts.Notifications;

/// <summary>
/// A browser the user gets push notifications on.
/// </summary>
public sealed class PushDevice
{
    /// <summary>
    /// The device's id.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// What it is, e.g. <c>Chrome on Android</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// When push was turned on for it, UTC.
    /// </summary>
    public required DateTime Added { get; init; }

    /// <summary>
    /// How the last push to it went, null when nothing has been pushed to it.
    /// </summary>
    public DeliveryStatus? LastPush { get; init; }
}
