using System.Text.Json.Serialization;

namespace FinanceTracker.Contracts.SavingsGoals;

/// <summary>
/// Which way tagged money moves when it's saved, for a goal tracking tags.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SavingsDirection>))]
public enum SavingsDirection
{
    /// <summary>
    /// Saved into an account here: tagged money in adds to the goal, money out takes from it.
    /// </summary>
    MoneyIn = 0,

    /// <summary>
    /// Saved somewhere FinTrack can't see: tagged money out adds to the goal, money back in takes from it.
    /// </summary>
    MoneyOut = 1
}
