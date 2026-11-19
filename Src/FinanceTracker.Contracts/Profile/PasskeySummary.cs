namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// One of the user's passkeys, as their profile lists them.
/// </summary>
public sealed class PasskeySummary
{
    /// <summary>
    /// The passkey's id, base64url, to rename or remove it by.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The name the user gave it, e.g. <c>Work laptop</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// When it was added.
    /// </summary>
    public required DateTimeOffset AddedAt { get; init; }

    /// <summary>
    /// Whether it's synced to the user's other devices, by their password manager or phone's account.
    /// </summary>
    public required bool Synced { get; init; }
}
