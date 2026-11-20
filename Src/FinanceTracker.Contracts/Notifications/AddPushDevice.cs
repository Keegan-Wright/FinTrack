namespace FinanceTracker.Contracts.Notifications;

/// <summary>
/// A browser's push subscription, as it gives it when push is turned on, see
/// <see cref="Validation.AddPushDeviceValidator"/>.
/// </summary>
public sealed class AddPushDevice
{
    /// <summary>
    /// Where its push service takes messages for it.
    /// </summary>
    public string Endpoint { get; set; } = "";

    /// <summary>
    /// Its <c>p256dh</c> key, base64url.
    /// </summary>
    public string PublicKey { get; set; } = "";

    /// <summary>
    /// Its <c>auth</c> secret, base64url.
    /// </summary>
    public string AuthSecret { get; set; } = "";

    /// <summary>
    /// What it is, e.g. <c>Chrome on Android</c>.
    /// </summary>
    public string Name { get; set; } = "";
}
