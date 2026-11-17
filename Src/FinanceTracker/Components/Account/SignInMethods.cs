namespace FinanceTracker.Components.Account;

/// <summary>
/// The ways people can sign in, at least one of them on.
/// </summary>
public sealed class SignInMethods
{
    /// <summary>
    /// Signing in, registering and resetting passwords with a username and password.
    /// </summary>
    public required bool Password { get; init; }

    /// <summary>
    /// Signing in through the identity provider, which also makes new users.
    /// </summary>
    public required bool SingleSignOn { get; init; }

    /// <summary>
    /// Whether <paramref name="hasPassword"/> is any use, as one can't be used with password sign in turned off.
    /// </summary>
    /// <param name="hasPassword">Whether the user has a password.</param>
    public bool CanUsePassword(bool hasPassword) => Password && hasPassword;
}
