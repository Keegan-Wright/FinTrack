using FinanceTracker.Domain.Classifications;

namespace FinanceTracker.Domain.Rules;

/// <summary>
/// Something a <see cref="Rule"/> does to each transaction it matches.
/// </summary>
/// <param name="Type">What it does.</param>
/// <param name="ClassificationId">The tag <see cref="RuleActionType.AddTag"/> adds, null for the other types.</param>
/// <param name="BankClassification">The bank's classification <see cref="RuleActionType.HideClassification"/> hides,
/// null for the other types.</param>
public sealed record RuleAction(RuleActionType Type, Guid? ClassificationId, string? BankClassification = null)
{
    public static RuleAction AddTag(CustomClassification classification)
    {
        ArgumentNullException.ThrowIfNull(classification);
        return new RuleAction(RuleActionType.AddTag, classification.Id);
    }

    public static RuleAction MarkAsTransfer() => new(RuleActionType.MarkAsTransfer, null);

    public static RuleAction MarkNotATransfer() => new(RuleActionType.MarkNotATransfer, null);

    /// <param name="bankClassification">As the bank sends it, matched in any case.</param>
    public static RuleAction HideClassification(string bankClassification)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bankClassification);
        return new RuleAction(RuleActionType.HideClassification, null, bankClassification.Trim());
    }

    /// <summary>
    /// Whether this decides the transfer answer, which only one action on a rule can.
    /// </summary>
    public bool DecidesTransfer => Type is RuleActionType.MarkAsTransfer or RuleActionType.MarkNotATransfer;
}
