namespace FinanceTracker.Infrastructure.Notifications.Push;

/// <summary>
/// The server's <see cref="VapidKeys"/> as saved, one row, made the first time a browser subscribes. Never replaced
/// by the app, as every browser's subscription is tied to the public key.
/// </summary>
public sealed class StoredVapidKeys
{
    /// <summary>
    /// The one row's id.
    /// </summary>
    public const int OnlyId = 1;

    /// <summary>
    /// Always <see cref="OnlyId"/>, so two servers starting at once can't both add a pair.
    /// </summary>
    public int Id { get; init; } = OnlyId;

    /// <summary>
    /// The public key, uncompressed, in base64url.
    /// </summary>
    public required string PublicKey { get; init; }

    /// <summary>
    /// The private key in base64url. Encrypted, anyone with it could push to every subscribed browser.
    /// </summary>
    public required string PrivateKey { get; init; }

    /// <summary>
    /// When it was made, in UTC.
    /// </summary>
    public DateTime Created { get; init; }
}
