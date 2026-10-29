namespace FinanceTracker.Domain.Rules;

/// <summary>
/// What a user's rules would do to one transaction, from <see cref="RuleEvaluator.Evaluate"/>. Nothing has changed
/// here so it can be shown as a preview.
/// </summary>
/// <param name="Tags">The tags to add, each with the rule that added it.</param>
/// <param name="Transfer">The transfer answer, null when no rule gave one.</param>
public sealed record RuleOutcome(IReadOnlyList<RuleTag> Tags, RuleTransfer? Transfer)
{
    public static RuleOutcome None { get; } = new([], null);
}
