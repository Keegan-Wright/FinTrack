using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.Debts;

namespace FinanceTracker.Domain.Rules;

/// <summary>
/// Something a <see cref="Rule"/> does to each transaction it matches.
/// </summary>
/// <param name="Type">What it does.</param>
/// <param name="ClassificationId">The tag <see cref="RuleActionType.AddTag"/> adds, null for the other types.</param>
/// <param name="BankClassification">The bank's classification <see cref="RuleActionType.HideClassification"/> hides,
/// null for the other types.</param>
/// <param name="BillId">The bill <see cref="RuleActionType.PayBill"/> says it pays, null for the other types.</param>
/// <param name="DebtId">The debt <see cref="RuleActionType.PayDebt"/> says it pays off, null for the other types.
/// </param>
public sealed record RuleAction(
    RuleActionType Type,
    Guid? ClassificationId,
    string? BankClassification = null,
    Guid? BillId = null,
    Guid? DebtId = null)
{
    /// <summary>
    /// Adds <paramref name="classification"/> to the transaction.
    /// </summary>
    /// <param name="classification">The tag to add.</param>
    public static RuleAction AddTag(CustomClassification classification)
    {
        ArgumentNullException.ThrowIfNull(classification);
        return new RuleAction(RuleActionType.AddTag, classification.Id);
    }

    /// <summary>
    /// Says the transaction is a transfer between the user's own accounts.
    /// </summary>
    public static RuleAction MarkAsTransfer() => new(RuleActionType.MarkAsTransfer, null);

    /// <summary>
    /// Says the transaction isn't a transfer, so it counts as income or spending.
    /// </summary>
    public static RuleAction MarkNotATransfer() => new(RuleActionType.MarkNotATransfer, null);

    /// <param name="bankClassification">As the bank sends it, matched in any case.</param>
    public static RuleAction HideClassification(string bankClassification)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bankClassification);
        return new RuleAction(RuleActionType.HideClassification, null, bankClassification.Trim());
    }

    /// <summary>
    /// Says the transaction pays <paramref name="bill"/>.
    /// </summary>
    /// <param name="bill">The bill it pays.</param>
    public static RuleAction PayBill(Bill bill)
    {
        ArgumentNullException.ThrowIfNull(bill);
        return new RuleAction(RuleActionType.PayBill, null, null, bill.Id);
    }

    /// <summary>
    /// Says the transaction pays off <paramref name="debt"/>.
    /// </summary>
    /// <param name="debt">The debt it pays off.</param>
    public static RuleAction PayDebt(Debt debt)
    {
        ArgumentNullException.ThrowIfNull(debt);
        return new RuleAction(RuleActionType.PayDebt, null, null, null, debt.Id);
    }

    /// <summary>
    /// Whether this decides the transfer answer, which only one action on a rule can.
    /// </summary>
    public bool DecidesTransfer => Type is RuleActionType.MarkAsTransfer or RuleActionType.MarkNotATransfer;
}
