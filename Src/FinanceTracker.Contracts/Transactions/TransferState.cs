using System.Text.Json.Serialization;

namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// How a transaction counts, see <see cref="TransactionTransfer"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TransferState>))]
public enum TransferState
{
    /// <summary>
    /// Money moved between the user's own accounts or within their household, left out of income and spending.
    /// </summary>
    Transfer = 1,

    /// <summary>
    /// The user said it counts as income or spending even though it looked like a transfer.
    /// </summary>
    NotATransfer = 2,

    /// <summary>
    /// Might be a transfer, still counted until the user decides.
    /// </summary>
    NeedsReview = 3
}
