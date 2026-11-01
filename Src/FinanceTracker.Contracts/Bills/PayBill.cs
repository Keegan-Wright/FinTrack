using System.ComponentModel;

namespace FinanceTracker.Contracts.Bills;

/// <summary>
/// Which bill a transaction pays, as the user says.
/// </summary>
public class PayBill
{
    /// <summary>
    /// The bill it pays, null when it pays none so matching leaves it alone.
    /// </summary>
    [Description("The bill the transaction pays, null when it pays none")]
    public Guid? BillId { get; set; }
}
