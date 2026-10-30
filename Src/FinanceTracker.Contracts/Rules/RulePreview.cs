using System.ComponentModel;

namespace FinanceTracker.Contracts.Rules;

/// <summary>
/// What applying a rule to the user's existing transactions would change, before they agree to it.
/// </summary>
public class RulePreview
{
    /// <summary>
    /// How many transactions would change.
    /// </summary>
    [Description("How many transactions would change")]
    public int Changed { get; init; }

    /// <summary>
    /// How many it would tag but won't, as the user set their tags themselves.
    /// </summary>
    [Description("How many it would tag but won't, as the user set their tags themselves")]
    public int LeftAlone { get; init; }

    /// <summary>
    /// Every change, newest first.
    /// </summary>
    [Description("Every change, newest first")]
    public List<RulePreviewChange> Changes { get; init; } = [];
}
