namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// A new password for the signed in user, given their current one.
/// </summary>
public sealed class ChangePassword
{
    /// <summary>
    /// The password they sign in with now, so someone at their unlocked screen can't change it.
    /// </summary>
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>
    /// The password to sign in with from now on.
    /// </summary>
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>
    /// <see cref="NewPassword"/> typed again, so a typo doesn't lock them out.
    /// </summary>
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
