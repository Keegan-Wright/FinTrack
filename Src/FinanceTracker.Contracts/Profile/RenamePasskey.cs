namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// A new name for one of the user's passkeys.
/// </summary>
public sealed class RenamePasskey
{
    /// <summary>
    /// What to call it.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
