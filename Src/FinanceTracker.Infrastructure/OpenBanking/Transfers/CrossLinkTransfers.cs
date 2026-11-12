using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.OpenBanking.Transfers;

/// <summary>
/// Transfers between two linked people's accounts, once what's shared between them changes.
/// </summary>
internal static class CrossLinkTransfers
{
    /// <summary>
    /// Takes the transfer answer off <paramref name="first"/>'s and <paramref name="second"/>'s transactions paired
    /// with the other's that can't pair any more, the account between them no longer shared or the link ended. Even
    /// those a person confirmed, as the pair isn't theirs to see any more. Matching then decides about them again.
    /// Added to the context, saved by the caller.
    /// </summary>
    public static async Task ClearUnpairableAsync(FinanceTrackerContext context, Guid first, Guid second,
        CancellationToken cancellationToken)
    {
        List<Guid> both = [first, second];
        Dictionary<Guid, BankAccount> accounts = await context.BankAccounts.AsNoTracking()
            .Where(x => both.Contains(x.UserId))
            .ToDictionaryAsync(static x => x.Id, cancellationToken);
        List<Guid> accountIds = accounts.Keys.ToList();

        List<BankTransaction> paired = await context.BankTransactions
            .Where(x => both.Contains(x.UserId) && x.Transfer != null && x.Transfer.PartnerAccountId != null &&
                        accountIds.Contains(x.Transfer.PartnerAccountId.Value))
            .ToListAsync(cancellationToken);

        TransferGroup group = await TransferGroup.ForAsync(context, first, cancellationToken);
        foreach (BankTransaction transaction in paired)
        {
            if (!IsUnpairable(transaction, accounts, group))
            {
                continue;
            }

            transaction.ClearTransfer(DecisionSource.User);
        }
    }

    /// <summary>
    /// Whether <paramref name="transaction"/>'s other half is on another person's account it can no longer pair with.
    /// </summary>
    private static bool IsUnpairable(BankTransaction transaction, Dictionary<Guid, BankAccount> accounts,
        TransferGroup group)
    {
        if (transaction.Transfer?.PartnerAccountId is not { } partnerAccountId ||
            !accounts.TryGetValue(partnerAccountId, out BankAccount? partnerAccount))
        {
            return false;
        }

        return partnerAccount.UserId != transaction.UserId && !group.Pairing.CanReach(transaction, partnerAccount);
    }
}
