using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.Rules;

/// <summary>
/// What a user's rules would do to one transaction, from <see cref="RuleEvaluator.Evaluate"/>. Nothing has changed
/// here so it can be shown as a preview.
/// </summary>
/// <param name="Tags">The tags to add, each with the rule that added it.</param>
/// <param name="Transfer">The transfer answer, null when no rule gave one.</param>
/// <param name="HiddenClassifications">The bank's classifications to hide, each with the rule that hid it.</param>
public sealed record RuleOutcome(IReadOnlyList<RuleTag> Tags, RuleTransfer? Transfer,
    IReadOnlyList<HiddenClassification> HiddenClassifications)
{
    /// <summary>
    /// Tags and a transfer answer, hiding nothing.
    /// </summary>
    public RuleOutcome(IReadOnlyList<RuleTag> tags, RuleTransfer? transfer) : this(tags, transfer, [])
    {
    }

    public static RuleOutcome None { get; } = new([], null);
}
