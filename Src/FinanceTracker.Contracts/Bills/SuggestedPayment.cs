using System.ComponentModel;

namespace FinanceTracker.Contracts.Bills;

/// <summary>
/// One of the payments behind a <see cref="BillSuggestion"/>.
/// </summary>
public class SuggestedPayment
{
    /// <summary>
    /// The transaction's id.
    /// </summary>
    [Description("The transaction's id")]
    public Guid TransactionId { get; init; }

    /// <summary>
    /// The transaction's description.
    /// </summary>
    [Description("The transaction's description")]
    public required string Description { get; init; }

    /// <summary>
    /// Money out, as the bank reports it.
    /// </summary>
    [Description("The transaction's amount")]
    public decimal Amount { get; init; }

    /// <summary>
    /// When the transaction happened.
    /// </summary>
    [Description("When the transaction happened")]
    public DateTime OccurredAt { get; init; }
}
