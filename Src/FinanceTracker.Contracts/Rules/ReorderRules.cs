using System.ComponentModel;

namespace FinanceTracker.Contracts.Rules;

/// <summary>
/// The order the user's rules run in.
/// </summary>
public class ReorderRules
{
    /// <summary>
    /// Every one of the user's rules, first to run first.
    /// </summary>
    [Description("Every one of the user's rule ids, first to run first")]
    public List<Guid> RuleIds { get; set; } = [];
}
