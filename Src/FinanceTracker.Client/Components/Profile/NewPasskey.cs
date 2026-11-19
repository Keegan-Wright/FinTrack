namespace FinanceTracker.Client.Components.Profile;

/// <summary>
/// What the user fills in before their browser makes a passkey.
/// </summary>
public sealed class NewPasskey
{
    /// <summary>
    /// What to call it, e.g. <c>Work laptop</c>.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Their password, as adding a passkey changes how they sign in.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
