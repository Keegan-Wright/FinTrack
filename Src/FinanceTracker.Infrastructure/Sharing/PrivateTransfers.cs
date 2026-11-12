using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;

namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// Money moved between an account someone shares and one of theirs they keep private. The other person sees the amount,
/// it really did move on the shared account, but not the bank's description, which may name what the private account
/// is for.
/// </summary>
public static class PrivateTransfers
{
    public const string ToPrivateAccount = "Transfer to a private account";

    public const string FromPrivateAccount = "Transfer from a private account";

    public const string MaybeToPrivateAccount = "Possible transfer to a private account";

    public const string MaybeFromPrivateAccount = "Possible transfer from a private account";

    extension(AccountsInView view)
    {
        /// <summary>
        /// Whether <paramref name="transaction"/> is on an account shared with the user, and is, or may be, a transfer
        /// with an account they can't see: one its owner keeps private, or someone else's. Not one of the user's own,
        /// money they moved into the shared account themselves. See <see cref="IsPossiblePrivateTransfer"/> for one
        /// whose other half isn't known.
        /// </summary>
        public bool IsPrivateTransfer(BankTransaction transaction)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            if (SharedTransfer(view, transaction) is not { PartnerAccountId: { } partnerAccountId })
            {
                return false;
            }

            return !view.Contains(partnerAccountId);
        }

        /// <summary>
        /// Whether <paramref name="transaction"/> is on an account shared with the user and is, or may be, a transfer
        /// whose other half isn't known, while its owner has accounts the user can't see. The other half may not have
        /// synced yet, or several could fit, so it may have gone to one of them. A payment to someone else the bank
        /// calls a transfer is hidden too, there's no telling them apart.
        /// </summary>
        public bool IsPossiblePrivateTransfer(BankTransaction transaction)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            if (SharedTransfer(view, transaction) is not { PartnerAccountId: null, PartnerHouseholdMemberId: null })
            {
                return false;
            }

            return view.OwnersWithPrivateAccounts.Contains(transaction.UserId);
        }

        /// <summary>
        /// The description the user sees, the bank's unless it's a <see cref="IsPrivateTransfer">private
        /// transfer</see> or a <see cref="IsPossiblePrivateTransfer">possible one</see>.
        /// </summary>
        public string DescriptionOf(BankTransaction transaction)
        {
            bool moneyOut = transaction.Amount < 0;
            if (view.IsPrivateTransfer(transaction))
            {
                return moneyOut ? ToPrivateAccount : FromPrivateAccount;
            }

            if (view.IsPossiblePrivateTransfer(transaction))
            {
                return moneyOut ? MaybeToPrivateAccount : MaybeFromPrivateAccount;
            }

            return transaction.Description;
        }
    }

    /// <summary>
    /// <paramref name="transaction"/>'s transfer when it's on an account shared with the user and is, or may be, one.
    /// The other person's copy of a joint account filling a gap counts as shared, it's theirs as much as one they
    /// share.
    /// </summary>
    private static TransferInfo? SharedTransfer(AccountsInView view, BankTransaction transaction)
    {
        bool othersFillingCopy = transaction.UserId != view.UserId &&
                                 view.FillingAccounts.ContainsKey(transaction.AccountId);
        if (!view.IsShared(transaction.AccountId) && !othersFillingCopy)
        {
            return null;
        }

        if (transaction.Transfer is not { } transfer || transfer.Decision == TransferDecision.NotATransfer)
        {
            return null;
        }

        return transfer;
    }
}
