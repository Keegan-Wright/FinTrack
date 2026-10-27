namespace FinanceTracker.Domain.Common;

/// <summary>
/// Who made a decision about a transaction, ordered by precedence. A decision only replaces one from an equal or lower
/// source, so the matcher never overrides a rule and a rule never overrides the user.
/// </summary>
public enum DecisionSource
{
    Matcher = 1,
    Rule = 2,
    User = 3
}
