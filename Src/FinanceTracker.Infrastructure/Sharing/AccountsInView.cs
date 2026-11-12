namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// The accounts a user sees in the money based views their own, and those shared with them unless they've chosen to see
/// only theirs, see <see cref="IAccountScope"/>.
/// </summary>
/// <param name="UserId">Whose view it is, their own accounts are always in it.</param>
/// <param name="SharedAccountIds">Accounts other people share with them that are in view, none for Just mine.</param>
/// <param name="SharedOwners">Who owns each of <paramref name="SharedAccountIds"/>, by account.</param>
public sealed record AccountsInView(
    Guid UserId,
    IReadOnlyList<Guid> SharedAccountIds,
    IReadOnlyDictionary<Guid, Guid> SharedOwners)
{
    /// <summary>
    /// The people whose accounts are in view besides the user.
    /// </summary>
    public IReadOnlyList<Guid> SharedOwnerIds { get; } = SharedOwners.Values.Distinct().ToList();

    public bool IsShared(Guid accountId) => SharedOwners.ContainsKey(accountId);
}
