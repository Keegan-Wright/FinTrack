using System.ComponentModel;

namespace FinanceTracker.Contracts.Debts;

/// <summary>
/// Which debt a transaction pays off, as the user says.
/// </summary>
public class PayDebt
{
    /// <summary>
    /// The debt it pays off, null when it pays none so rules leave it alone.
    /// </summary>
    [Description("The debt the transaction pays off, null when it pays none")]
    public Guid? DebtId { get; set; }
}
