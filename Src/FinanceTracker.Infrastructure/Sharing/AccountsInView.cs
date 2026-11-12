namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// The accounts a user sees, or can change, see <see cref="IAccountScope"/>: their own, and those shared with them.
/// </summary>
/// <param name="UserId">Whose view it is, their own accounts are always in it.</param>
/// <param name="OwnAccountIds">Their own accounts, the hidden copies of joint accounts aside.</param>
/// <param name="SharedOwners">Who owns each account other people share with them that's in it, by account.</param>
/// <param name="ChangeableAccountIds">Those of the shared accounts they're allowed to change.</param>
/// <param name="OwnersWithPrivateAccounts">Those of <see cref="SharedOwnerIds"/> who have accounts the user can't
/// see, which a transfer on a shared account may have gone to.</param>
/// <param name="HiddenAccountIds">Their own copies of joint accounts, left out as the other person's copy is shown
/// instead, see <see cref="Domain.Sharing.JointAccount"/>.</param>
/// <param name="FillingAccounts">The hidden copies of the joint accounts in it, by hidden copy, each with its shown
/// copy. Their transactions that <see cref="Domain.OpenBanking.Transactions.BankTransaction.FillsJointGap">fill a
/// gap</see> are in it as the shown copy's.</param>
/// <param name="IsJustMine">Whether it's the user's Just mine view, which leaves out what others share, joint accounts
/// aside.</param>
public sealed record AccountsInView(
    Guid UserId,
    IReadOnlySet<Guid> OwnAccountIds,
    IReadOnlyDictionary<Guid, Guid> SharedOwners,
    IReadOnlySet<Guid> ChangeableAccountIds,
    IReadOnlySet<Guid> OwnersWithPrivateAccounts,
    IReadOnlySet<Guid> HiddenAccountIds,
    IReadOnlyDictionary<Guid, Guid> FillingAccounts,
    bool IsJustMine = false)
{
    /// <summary>
    /// The accounts other people share with them that are in it, none for Just mine.
    /// </summary>
    public IReadOnlyList<Guid> SharedAccountIds { get; } = SharedOwners.Keys.ToList();

    /// <summary>
    /// The people whose accounts are in it besides the user.
    /// </summary>
    public IReadOnlyList<Guid> SharedOwnerIds { get; } = SharedOwners.Values.Distinct().ToList();

    public bool IsShared(Guid accountId) => SharedOwners.ContainsKey(accountId);

    /// <summary>
    /// The hidden copies in <see cref="FillingAccounts"/>, for queries.
    /// </summary>
    public IReadOnlyList<Guid> FillingAccountIds { get; } = FillingAccounts.Keys.ToList();

    /// <summary>
    /// Whether <paramref name="accountId"/> is the user's own or shared with them here, or the hidden copy of a joint
    /// account here whose transactions may fill in for it.
    /// </summary>
    public bool Contains(Guid accountId) =>
        OwnAccountIds.Contains(accountId) || IsShared(accountId) || FillingAccounts.ContainsKey(accountId);

    /// <summary>
    /// The account <paramref name="accountId"/>'s transactions show as: the shown copy for the hidden copy of a joint
    /// account, whose transactions only show when they fill a gap, otherwise itself.
    /// </summary>
    public Guid ShownAs(Guid accountId) => FillingAccounts.GetValueOrDefault(accountId, accountId);

    /// <summary>
    /// <paramref name="accountIds"/> with the hidden copies that fill in for any of them, for filtering transactions
    /// by account. Null stays null, for every account.
    /// </summary>
    public IReadOnlyCollection<Guid>? WithFillingCopies(IReadOnlyCollection<Guid>? accountIds)
    {
        if (accountIds is null)
        {
            return null;
        }

        return
        [
            .. accountIds,
            .. FillingAccounts.Where(x => accountIds.Contains(x.Value)).Select(static x => x.Key)
        ];
    }

    /// <summary>
    /// Whether the user can see <paramref name="accountId"/>'s transactions but not change them.
    /// </summary>
    public bool IsReadOnly(Guid accountId)
    {
        Guid shown = ShownAs(accountId);
        return IsShared(shown) && !ChangeableAccountIds.Contains(shown);
    }
}
