using System.ComponentModel;

namespace FinanceTracker.Models.Response.Transaction;

public class TransactionResponse
{
    [Description("Unique identifier for the transaction")]
    public required Guid TransactionId { get; init; }

    [Description("Description of the transaction")]
    public required string Description { get; init; }

    [Description("Type of the transaction (e.g., income, expense)")]
    public required string TransactionType { get; init; }

    [Description("Category of the transaction (e.g., groceries, utilities)")]
    public required string TransactionCategory { get; init; }

    [Description("Amount of the transaction")]
    public required decimal Amount { get; init; }

    [Description("Currency code of the transaction")]
    public required string Currency { get; init; }

    [Description("Date and time when the transaction occurred")]
    public required DateTime TransactionTime { get; init; }

    [Description("Indicates if the transaction is pending")]
    public required bool Pending { get; init; }

    [Description("List of tags associated with the transaction")]
    public required IEnumerable<string> Tags { get; set; }
}
