using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.OpenBanking.Transfers;

/// <summary>
/// Which transactions the <see cref="TransferMatcher"/> may pair. Always two of one person's, and two people's only
/// when one of the two accounts is shared with the other person, money moving into a joint account someone shares
/// with their partner for example. Without that, a payment to someone stays their spending and the other's income.
/// </summary>
public sealed class TransferPairing
{
    private readonly HashSet<(Guid AccountId, Guid SharedWith)> _shared;

    /// <param name="shared">Each account shared through an accepted link, with who it's shared with.</param>
    public TransferPairing(IEnumerable<(Guid AccountId, Guid SharedWith)> shared)
    {
        ArgumentNullException.ThrowIfNull(shared);
        _shared = shared.ToHashSet();
    }

    /// <summary>
    /// Only one person's own accounts, as before anything was shared.
    /// </summary>
    public static TransferPairing OwnAccountsOnly { get; } = new([]);

    /// <summary>
    /// Whether <paramref name="first"/> and <paramref name="second"/> could be the two halves of one transfer.
    /// </summary>
    public bool CanPair(BankTransaction first, BankTransaction second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return CanReach(first.UserId, first.AccountId, second.UserId, second.AccountId);
    }

    /// <summary>
    /// Whether <paramref name="transaction"/> could have moved money to or from <paramref name="account"/>.
    /// </summary>
    public bool CanReach(BankTransaction transaction, BankAccount account)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(account);
        return CanReach(transaction.UserId, transaction.AccountId, account.UserId, account.Id);
    }

    private bool CanReach(Guid firstOwner, Guid firstAccount, Guid secondOwner, Guid secondAccount)
    {
        if (firstOwner == secondOwner)
        {
            return true;
        }

        return _shared.Contains((firstAccount, secondOwner)) || _shared.Contains((secondAccount, firstOwner));
    }
}
