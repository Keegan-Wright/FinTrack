namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// The accounts a user sees, or can change, see <see cref="IAccountScope"/>: their own, and those shared with them.
/// </summary>
/// <param name="UserId">Whose view it is, their own accounts are always in it.</param>
/// <param name="SharedOwners">Who owns each account other people share with them that's in it, by account.</param>
/// <param name="ChangeableAccountIds">Those of the shared accounts they're allowed to change.</param>
public sealed record AccountsInView(
    Guid UserId,
    IReadOnlyDictionary<Guid, Guid> SharedOwners,
    IReadOnlySet<Guid> ChangeableAccountIds)
{
    /// <summary>
    /// The accounts other people share with them that are in it, none for Just mine.
    /// </summary>
    public IReadOnlyList<Guid> SharedAccountIds { get; } = SharedOwners.Keys.ToList();

    /// <summary>
    /// The people whose accounts are in it besides the user.
    /// </summary>
    public IReadOnlyList<Guid> SharedOwnerIds { get; } = SharedOwners.Values.Distinct().ToList();

    /// <summary>
    /// Only the user's own accounts.
    /// </summary>
    public static AccountsInView Mine(Guid userId) => new(userId, new Dictionary<Guid, Guid>(), new HashSet<Guid>());

    public bool IsShared(Guid accountId) => SharedOwners.ContainsKey(accountId);

    /// <summary>
    /// Whether the user can see <paramref name="accountId"/>'s transactions but not change them.
    /// </summary>
    public bool IsReadOnly(Guid accountId) => IsShared(accountId) && !ChangeableAccountIds.Contains(accountId);
}
