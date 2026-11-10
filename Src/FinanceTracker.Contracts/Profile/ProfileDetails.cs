namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// The signed in user's own account who they are, their email, and how they sign in.
/// </summary>
public sealed class ProfileDetails
{
    /// <summary>
    /// The name they sign in with.
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// The account's email, null when it has none.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Whether the user has confirmed <see cref="Email"/> is theirs, by the link emailed to it. Notifications and
    /// password resets are only emailed to a confirmed one.
    /// </summary>
    public required bool EmailConfirmed { get; init; }

    /// <summary>
    /// Whether the account has a password here. One made by signing in through the identity provider hasn't, its
    /// password and email are the provider's, so neither changes here.
    /// </summary>
    public required bool HasPassword { get; init; }

    /// <summary>
    /// Whether the account can sign in through the identity provider.
    /// </summary>
    public required bool UsesIdentityProvider { get; init; }

    /// <summary>
    /// Whether this FinTrack can send email at all, set up by whoever runs it.
    /// </summary>
    public required bool MailAvailable { get; init; }

    /// <summary>
    /// Whether the user should be asked to confirm their email: there's one to confirm and a way to send the link.
    /// </summary>
    public bool NeedsConfirming => MailAvailable && Email is not null && !EmailConfirmed;
}
