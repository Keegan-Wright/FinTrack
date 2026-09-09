using System.ComponentModel;

namespace FinanceTracker.Contracts.Transaction;

public class TransactionTagFilterResponse
{
    [Description("Tag name for filtering transactions")]
    public required string Tag { get; init; }
}
