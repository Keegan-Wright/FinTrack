namespace FinanceTracker.Domain.Rules;

/// <summary>
/// How many of a <see cref="Rule"/>'s conditions a transaction has to meet.
/// </summary>
public enum RuleMatchMode
{
    /// <summary>
    /// Every condition.
    /// </summary>
    All = 1,
    /// <summary>
    /// At least one condition.
    /// </summary>
    Any = 2
}
