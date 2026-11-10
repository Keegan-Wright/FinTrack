namespace FinanceTracker.Contracts.Users;

/// <summary>
/// Someone who can sign in to this FinTrack, as an admin's users page lists them.
/// </summary>
public sealed class UserSummary
{
    /// <summary>
    /// The user's id, to make them a reset link by.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The name they sign in with.
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// Their email, null when they have none.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Whether they've confirmed <see cref="Email"/> is theirs.
    /// </summary>
    public required bool EmailConfirmed { get; init; }

    /// <summary>
    /// Whether they have a password here, so a reset link can be made for them. One made by signing in through the
    /// identity provider hasn't, their password is the provider's.
    /// </summary>
    public required bool HasPassword { get; init; }

    /// <summary>
    /// Whether they can sign in through the identity provider.
    /// </summary>
    public required bool UsesIdentityProvider { get; init; }

    /// <summary>
    /// Whether they're an admin.
    /// </summary>
    public required bool IsAdmin { get; init; }

    /// <summary>
    /// Whether an admin has locked them, so they can't sign in and their banks aren't synced until they're unlocked.
    /// </summary>
    public required bool IsLocked { get; init; }

    /// <summary>
    /// Whether they're the admin looking, who can't lock themselves out.
    /// </summary>
    public required bool IsYou { get; init; }
}
