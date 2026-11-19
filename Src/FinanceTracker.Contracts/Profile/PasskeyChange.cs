namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// How a change to the user's passkeys went.
/// </summary>
public sealed class PasskeyChange
{
    /// <summary>
    /// Why it wasn't made, in words for the user. Empty when it was.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// What the browser needs to make a new passkey, as WebAuthn JSON. Only when one is being added.
    /// </summary>
    public string? CreationOptionsJson { get; init; }

    /// <summary>
    /// Whether the change was made.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;
}
