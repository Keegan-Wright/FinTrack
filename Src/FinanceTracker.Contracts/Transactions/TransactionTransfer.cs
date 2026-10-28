using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// Whether a transaction is money moved between the user's own accounts or within their household, why, and where
/// the money went or came from.
/// </summary>
public class TransactionTransfer
{
    /// <summary>
    /// How it counts.
    /// </summary>
    [Description("Whether the transaction is a transfer, not one, or waiting for review")]
    public required TransferState State { get; init; }

    /// <summary>
    /// Why FinTrack decided, null for the user's own decisions without one.
    /// </summary>
    [Description("Why FinTrack decided it")]
    public TransferMatchReason? Reason { get; init; }

    /// <summary>
    /// Who decided.
    /// </summary>
    [Description("Who decided whether it is a transfer")]
    public required TransferDecidedBy DecidedBy { get; init; }

    /// <summary>
    /// The other half of the transfer, or for review the suggested one.
    /// </summary>
    [Description("The other half of the transfer")]
    public Guid? PartnerTransactionId { get; init; }

    /// <summary>
    /// The other half's description.
    /// </summary>
    [Description("Description of the other half of the transfer")]
    public string? PartnerDescription { get; init; }

    /// <summary>
    /// When the other half happened.
    /// </summary>
    [Description("When the other half of the transfer happened")]
    public DateTime? PartnerTime { get; init; }

    /// <summary>
    /// The account the money moved to or from.
    /// </summary>
    [Description("The account the money moved to or from")]
    public Guid? PartnerAccountId { get; init; }

    /// <summary>
    /// That account's name.
    /// </summary>
    [Description("Name of the account the money moved to or from")]
    public string? PartnerAccountName { get; init; }

    /// <summary>
    /// The household member the money moved to or from, when their account isn't connected.
    /// </summary>
    [Description("Name of the household member the money moved to or from")]
    public string? PartnerHouseholdMemberName { get; init; }
}
