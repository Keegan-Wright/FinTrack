using System.Text.Json.Serialization;

namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// Who decided whether a transaction is a transfer. The user's decisions are never changed by FinTrack.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TransferDecidedBy>))]
public enum TransferDecidedBy
{
    /// <summary>
    /// FinTrack's matching, which may change its mind as transactions arrive.
    /// </summary>
    FinTrack = 1,

    /// <summary>
    /// One of the user's rules.
    /// </summary>
    Rule = 2,

    /// <summary>
    /// The user.
    /// </summary>
    User = 3
}
