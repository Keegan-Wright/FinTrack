namespace FinanceTracker.Domain.Common;

/// <summary>
/// Who made a decision about a transaction, ordered by precedence. A decision only replaces one from an equal or lower
/// source, so the matcher never overrides a rule and a rule never overrides the user.
/// </summary>
public enum DecisionSource
{
    /// <summary>
    /// Matched automatically during a sync.
    /// </summary>
    Matcher = 1,
    /// <summary>
    /// One of the user's rules.
    /// </summary>
    Rule = 2,
    /// <summary>
    /// The user themselves. Nothing else overrides it.
    /// </summary>
    User = 3
}
