using System.Text.Json.Serialization;

namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// Why FinTrack decided what it did about a transfer.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TransferMatchReason>))]
public enum TransferMatchReason
{
    /// <summary>
    /// The bank's reference names another of the user's accounts.
    /// </summary>
    OwnAccountReference = 1,

    /// <summary>
    /// An equal and opposite amount on another of the user's accounts within a couple of days.
    /// </summary>
    MirrorPair = 2,

    /// <summary>
    /// Several equally good other halves, so none was picked.
    /// </summary>
    AmbiguousMirrorPair = 3,

    /// <summary>
    /// The bank labels it a transfer but no other half was found.
    /// </summary>
    BankTransferLabel = 4,

    /// <summary>
    /// Money to or from one of the user's household members.
    /// </summary>
    HouseholdMember = 5
}
