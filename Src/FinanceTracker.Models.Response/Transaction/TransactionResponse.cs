using System.ComponentModel;

namespace FinanceTracker.Models.Response.Transaction;

public class TransactionResponse
{
    [Description("Unique identifier for the transaction")]
    public required Guid TransactionId { get; set; }

    [Description("Description of the transaction")]
    public required string Description { get; set; }

    [Description("Type of the transaction (e.g., income, expense)")]
    public required string TransactionType { get; set; }

    [Description("Category of the transaction (e.g., groceries, utilities)")]
    public required string TransactionCategory { get; set; }

    [Description("Amount of the transaction")]
    public required decimal Amount { get; set; }

    [Description("Currency code of the transaction")]
    public required string Currency { get; set; }

    [Description("Date and time when the transaction occurred")]
    public required DateTime TransactionTime { get; set; }

    [Description("Indicates if the transaction is pending")]
    public required bool Pending { get; set; }

    [Description("List of tags associated with the transaction")]
    public IEnumerable<string> Tags { get; set; }
}