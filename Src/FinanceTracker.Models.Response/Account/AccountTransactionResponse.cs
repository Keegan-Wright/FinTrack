using System.ComponentModel;

namespace FinanceTracker.Models.Response.Account;

public class AccountTransactionResponse
{
    [Description("Description of the transaction")]
    public string Description { get; set; }

    [Description("Amount of the transaction")]
    public decimal Amount { get; set; }

    [Description("Time when the transaction occurred")]
    public DateTime Time { get; set; }

    [Description("Status of the transaction")]
    public string Status { get; set; }
}
