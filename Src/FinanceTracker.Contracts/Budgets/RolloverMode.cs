using System.Text.Json.Serialization;

namespace FinanceTracker.Contracts.Budgets;

/// <summary>
/// What a budget carries from one month into the next.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RolloverMode>))]
public enum RolloverMode
{
    /// <summary>
    /// Each month starts afresh at its limit.
    /// </summary>
    None = 1,

    /// <summary>
    /// What wasn't spent is added to the next month, overspending isn't taken from it.
    /// </summary>
    CarryUnderspend = 2,

    /// <summary>
    /// What wasn't spent is added to the next month and overspending is taken from it.
    /// </summary>
    CarryBoth = 3
}
