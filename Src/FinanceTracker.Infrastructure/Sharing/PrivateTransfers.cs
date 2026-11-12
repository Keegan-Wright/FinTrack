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

    extension(AccountsInView view)
    {
        /// <summary>
        /// Whether <paramref name="transaction"/> is on an account shared with the user, and is, or may be, a transfer
        /// with one of its owner's accounts that isn't. Partners are always the owner's own accounts, transfers are
        /// matched between one person's accounts.
        /// </summary>
        public bool IsPrivateTransfer(BankTransaction transaction)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            if (!view.IsShared(transaction.AccountId))
            {
                return false;
            }

            if (transaction.Transfer is not { PartnerAccountId: { } partnerAccountId } transfer ||
                transfer.Decision == TransferDecision.NotATransfer)
            {
                return false;
            }

            return !view.IsShared(partnerAccountId);
        }

        /// <summary>
        /// The description the user sees, the bank's unless it's a <see cref="IsPrivateTransfer">private
        /// transfer</see>.
        /// </summary>
        public string DescriptionOf(BankTransaction transaction)
        {
            if (!view.IsPrivateTransfer(transaction))
            {
                return transaction.Description;
            }

            if (transaction.Amount < 0)
            {
                return ToPrivateAccount;
            }

            return FromPrivateAccount;
        }
    }
}
