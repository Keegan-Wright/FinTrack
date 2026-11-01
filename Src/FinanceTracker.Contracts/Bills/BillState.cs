using System.Text.Json.Serialization;

namespace FinanceTracker.Contracts.Bills;

/// <summary>
/// How one period of a bill stands.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BillState>))]
public enum BillState
{
    /// <summary>
    /// Not paid yet, and not more than a few days past due.
    /// </summary>
    Upcoming = 1,

    /// <summary>
    /// Paid about what was expected.
    /// </summary>
    Paid = 2,

    /// <summary>
    /// More than a few days past due with no payment.
    /// </summary>
    Late = 3,

    /// <summary>
    /// Paid, but a different amount than expected, the price has likely changed.
    /// </summary>
    Changed = 4
}
