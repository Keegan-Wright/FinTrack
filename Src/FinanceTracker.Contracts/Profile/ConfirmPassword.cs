namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// The signed in user's password, asked for again before a change to how they sign in.
/// </summary>
public sealed class ConfirmPassword
{
    /// <summary>
    /// The password they sign in with.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
