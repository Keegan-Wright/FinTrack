using System.ComponentModel;

namespace FinanceTracker.Contracts.Classifications;

/// <summary>
/// Custom classifications to apply to a transaction. Validated by <see
/// cref="Validation.AddCustomClassificationsToTransactionRequestValidator"/>.
/// </summary>
public class AddCustomClassificationsToTransaction
{
    /// <summary>
    /// The transaction, see <see cref="Transactions.Transaction.TransactionId"/>.
    /// </summary>
    [Description("Unique identifier of the transaction to classify")]
    public required Guid TransactionId { get; set; }

    /// <summary>
    /// The classifications to apply.
    /// </summary>
    [Description("Collection of classifications to apply to the transaction")]
    public required IEnumerable<SelectedCustomClassification> Classifications { get; set; }
}
