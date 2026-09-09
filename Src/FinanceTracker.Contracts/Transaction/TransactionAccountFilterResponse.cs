using System.ComponentModel;

namespace FinanceTracker.Contracts.Transaction;

public class TransactionAccountFilterResponse
{
    [Description("Unique identifier of the account")]
    public required Guid AccountId { get; init; }

    [Description("Name of the account")]
    public required string AccountName { get; init; }
}
