using System.Text.Json.Serialization;

namespace FinanceTracker.Contracts;

/// <summary>
/// Kinds of data synced from the open banking provider. Flags, so they can be combined.
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter<SyncTypes>))]
public enum SyncTypes
{
    /// <summary>
    /// The accounts themselves.
    /// </summary>
    Account = 1,

    /// <summary>
    /// Each account's balance.
    /// </summary>
    Balance = 2,

    /// <summary>
    /// Each account's direct debits.
    /// </summary>
    DirectDebits = 4,

    /// <summary>
    /// Each account's standing orders.
    /// </summary>
    StandingOrders = 8,

    /// <summary>
    /// Each account's booked and pending transactions.
    /// </summary>
    Transactions = 16,

    /// <summary>
    /// Everything. Its own flag rather than every other flag combined, check for it explicitly.
    /// </summary>
    All = 64
}
