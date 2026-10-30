using System.ComponentModel;

namespace FinanceTracker.Contracts.Rules;

/// <summary>
/// One test a rule makes of a transaction.
/// </summary>
public class RuleCondition
{
    /// <summary>
    /// What to look at.
    /// </summary>
    [Description("The part of the transaction the condition looks at")]
    public RuleField Field { get; set; }

    /// <summary>
    /// How to compare it.
    /// </summary>
    [Description("How the field is compared with the value")]
    public RuleOperator Operator { get; set; }

    /// <summary>
    /// What to compare it with. A number is written with a <c>.</c> for the decimal point, a type as <c>Debit</c> or
    /// <c>Credit</c> and an account as its id.
    /// </summary>
    [Description("The value the field is compared with")]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// The top of the range for <see cref="RuleOperator.Between"/>, null otherwise.
    /// </summary>
    [Description("The top of the range, for between only")]
    public string? SecondValue { get; set; }

    /// <summary>
    /// Whether text comparisons ignore case.
    /// </summary>
    [Description("Whether text comparisons ignore case")]
    public bool IgnoreCase { get; set; } = true;
}
