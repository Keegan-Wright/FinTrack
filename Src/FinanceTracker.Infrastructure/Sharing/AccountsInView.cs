namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// The accounts a user sees, or can change, see <see cref="IAccountScope"/>: their own, and those shared with them.
/// </summary>
/// <param name="UserId">Whose view it is, their own accounts are always in it.</param>
/// <param name="OwnAccountIds">Their own accounts.</param>
/// <param name="SharedOwners">Who owns each account other people share with them that's in it, by account.</param>
/// <param name="ChangeableAccountIds">Those of the shared accounts they're allowed to change.</param>
/// <param name="OwnersWithPrivateAccounts">Those of <see cref="SharedOwnerIds"/> who have accounts the user can't
/// see, which a transfer on a shared account may have gone to.</param>
public sealed record AccountsInView(
    Guid UserId,
    IReadOnlySet<Guid> OwnAccountIds,
    IReadOnlyDictionary<Guid, Guid> SharedOwners,
    IReadOnlySet<Guid> ChangeableAccountIds,
    IReadOnlySet<Guid> OwnersWithPrivateAccounts)
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
    /// Only the user's own accounts, <paramref name="ownAccountIds"/>.
    /// </summary>
    public static AccountsInView Mine(Guid userId, IReadOnlySet<Guid> ownAccountIds) =>
        new(userId, ownAccountIds, new Dictionary<Guid, Guid>(), new HashSet<Guid>(), new HashSet<Guid>());

    public bool IsShared(Guid accountId) => SharedOwners.ContainsKey(accountId);

    /// <summary>
    /// Whether <paramref name="accountId"/> is the user's own or shared with them here.
    /// </summary>
    public bool Contains(Guid accountId) => OwnAccountIds.Contains(accountId) || IsShared(accountId);

    /// <summary>
    /// Whether the user can see <paramref name="accountId"/>'s transactions but not change them.
    /// </summary>
    public bool IsReadOnly(Guid accountId) => IsShared(accountId) && !ChangeableAccountIds.Contains(accountId);
}
