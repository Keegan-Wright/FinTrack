using System.Text.Json.Serialization;

namespace FinanceTracker.Contracts.Bills;

/// <summary>
/// Where a bill came from.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BillSource>))]
public enum BillSource
{
    /// <summary>
    /// Seeded from a standing order, its schedule and amount follow the bank's.
    /// </summary>
    StandingOrder = 1,

    /// <summary>
    /// Seeded from a direct debit, monthly from its last payment until the user says otherwise.
    /// </summary>
    DirectDebit = 2,

    /// <summary>
    /// A suggested subscription the user confirmed.
    /// </summary>
    Detected = 3,

    /// <summary>
    /// Added by the user.
    /// </summary>
    Manual = 4
}
