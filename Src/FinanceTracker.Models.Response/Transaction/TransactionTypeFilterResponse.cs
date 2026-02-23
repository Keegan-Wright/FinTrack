using System.ComponentModel;

namespace FinanceTracker.Models.Response.Transaction;

public class TransactionTypeFilterResponse
{
    [Description("Type name for filtering transactions")]
    public required string TransactionType { get; init; }
}
