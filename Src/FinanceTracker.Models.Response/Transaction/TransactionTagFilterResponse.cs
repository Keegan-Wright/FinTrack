using System.ComponentModel;

namespace FinanceTracker.Models.Response.Transaction;

public class TransactionTagFilterResponse
{
    [Description("Tag name for filtering transactions")]
    public required string Tag { get; set; }
}
