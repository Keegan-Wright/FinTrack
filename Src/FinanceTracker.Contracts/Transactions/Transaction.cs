using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// One of the user's transactions.
/// </summary>
public class Transaction
{
    /// <summary>
    /// The transaction's id.
    /// </summary>
    [Description("Unique identifier for the transaction")]
    public required Guid TransactionId { get; init; }

    /// <summary>
    /// The bank's description, usually the payee or merchant.
    /// </summary>
    [Description("Description of the transaction")]
    public required string Description { get; init; }

    /// <summary>
    /// <c>Debit</c> or <c>Credit</c>.
    /// </summary>
    [Description("Type of the transaction (e.g., income, expense)")]
    public required string TransactionType { get; init; }

    /// <summary>
    /// The provider's category, e.g. <c>PURCHASE</c> or <c>TRANSFER</c>.
    /// </summary>
    [Description("Category of the transaction (e.g., groceries, utilities)")]
    public required string TransactionCategory { get; init; }

    /// <summary>
    /// Signed in <see cref="Currency"/>: negative is money out, positive is money in.
    /// </summary>
    [Description("Amount of the transaction")]
    public required decimal Amount { get; init; }

    /// <summary>
    /// The ISO 4217 currency code, e.g. <c>GBP</c>.
    /// </summary>
    [Description("Currency code of the transaction")]
    public required string Currency { get; init; }

    /// <summary>
    /// When the transaction happened.
    /// </summary>
    [Description("Date and time when the transaction occurred")]
    public required DateTime TransactionTime { get; init; }

    /// <summary>
    /// Whether the bank hasn't settled it yet.
    /// </summary>
    [Description("Indicates if the transaction is pending")]
    public required bool Pending { get; init; }

    /// <summary>
    /// Provider classifications followed by the names of applied custom classifications.
    /// </summary>
    [Description("List of tags associated with the transaction")]
    public required IEnumerable<string> Tags { get; set; }
}
