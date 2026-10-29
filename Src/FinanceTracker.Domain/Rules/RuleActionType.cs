namespace FinanceTracker.Domain.Rules;

/// <summary>
/// What a <see cref="RuleAction"/> does to a matching transaction.
/// </summary>
public enum RuleActionType
{
    /// <summary>
    /// Adds a tag. Tags from every matching rule are added.
    /// </summary>
    AddTag = 1,

    /// <summary>
    /// Decides it's money moved between the user's own accounts, with no partner. The first matching rule to decide
    /// wins.
    /// </summary>
    MarkAsTransfer = 2,

    /// <summary>
    /// Decides it counts as income or spending. The first matching rule to decide wins.
    /// </summary>
    MarkNotATransfer = 3
}
