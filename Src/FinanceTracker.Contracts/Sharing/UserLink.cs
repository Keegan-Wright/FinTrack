namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// Someone the signed in user is linked with, or has an invite out with either way.
/// </summary>
public sealed record UserLink
{
    /// <summary>
    /// The link's id, for answering or ending it.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The other person's first and last name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The other person's username, which tells two people of the same name apart.
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// Whether they're linked, or which way an invite is waiting.
    /// </summary>
    public required LinkState State { get; init; }

    /// <summary>
    /// When the invite was sent, or when it was accepted once they're linked.
    /// </summary>
    public required DateTime Since { get; init; }

    /// <summary>
    /// How many of their accounts the signed in user shares with the other person, none until they're linked.
    /// </summary>
    public int AccountsYouShare { get; init; }

    /// <summary>
    /// How many accounts the other person shares with them, none until they're linked.
    /// </summary>
    public int AccountsTheyShare { get; init; }

    /// <summary>
    /// How many other things, such as tags, the signed in user chose to share with the other person.
    /// </summary>
    public int ItemsYouShare { get; init; }

    /// <summary>
    /// How many other things, such as tags, the other person chose to share with them.
    /// </summary>
    public int ItemsTheyShare { get; init; }

    /// <summary>
    /// How many joint accounts they've merged, see <see cref="LinkShares.JointAccounts"/>.
    /// </summary>
    public int JointAccounts { get; init; }

    /// <summary>
    /// How many accounts both of them connected could be merged, see <see cref="LinkShares.JointSuggestions"/>.
    /// </summary>
    public int JointSuggestions { get; init; }
}
