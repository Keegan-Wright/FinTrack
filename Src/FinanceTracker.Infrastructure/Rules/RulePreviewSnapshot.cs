using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;

namespace FinanceTracker.Infrastructure.Rules;

/// <summary>
/// What rules can change on a transaction, taken before and after applying them to tell what a preview would change.
/// </summary>
/// <param name="Tags">Its tags by id.</param>
/// <param name="Visible">The bank's classifications that aren't hidden.</param>
/// <param name="Transfer">Its transfer answer, null when nothing has decided.</param>
/// <param name="BillId">The bill it pays, null when it pays none.</param>
/// <param name="DebtId">The debt it pays off, null when it pays none.</param>
internal sealed record RulePreviewSnapshot(HashSet<Guid> Tags, HashSet<string> Visible, TransferDecision? Transfer,
    Guid? BillId, Guid? DebtId)
{
    public static RulePreviewSnapshot Of(BankTransaction transaction) => new(
        transaction.CustomClassificationIds.ToHashSet(),
        transaction.VisibleProviderClassifications.ToHashSet(),
        transaction.Transfer?.Decision,
        transaction.BillPayment?.BillId,
        transaction.DebtPayment?.DebtId);
}
