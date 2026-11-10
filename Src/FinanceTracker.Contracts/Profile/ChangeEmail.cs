namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// An email the signed in user wants their account to use instead.
/// </summary>
public sealed class ChangeEmail
{
    /// <summary>
    /// The email to change to.
    /// </summary>
    public string NewEmail { get; set; } = string.Empty;
}
