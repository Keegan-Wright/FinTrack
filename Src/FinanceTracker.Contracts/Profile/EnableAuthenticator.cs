namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// Turns on two-factor sign in once the user's authenticator app gives a code.
/// </summary>
public sealed class EnableAuthenticator
{
    /// <summary>
    /// The code the app shows, which proves it was set up right.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// The password they sign in with, so someone at their unlocked screen can't turn it on.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
