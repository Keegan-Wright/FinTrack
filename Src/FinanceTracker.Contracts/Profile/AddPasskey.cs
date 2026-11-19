namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// A passkey the browser has made, to add to the user's account.
/// </summary>
public sealed class AddPasskey
{
    /// <summary>
    /// The browser's new credential, as WebAuthn JSON.
    /// </summary>
    public string CredentialJson { get; set; } = string.Empty;

    /// <summary>
    /// What to call it, so the user can tell their passkeys apart.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
