namespace FinanceTracker.Domain.Rules;

/// <summary>
/// How many of a <see cref="Rule"/>'s conditions a transaction has to meet.
/// </summary>
public enum RuleMatchMode
{
    All = 1,
    Any = 2
}
