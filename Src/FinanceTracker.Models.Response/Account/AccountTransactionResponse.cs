using System.ComponentModel;

namespace FinanceTracker.Models.Response.Account;

public class AccountTransactionResponse
{
    [Description("Description of the transaction")]
    public required string Description { get; init; }

    [Description("Amount of the transaction")]
    public required decimal Amount { get; init; }

    [Description("Time when the transaction occurred")]
    public required DateTime Time { get; init; }

    [Description("Status of the transaction")]
    public required string Status { get; init; }
}
