using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// One part of a transaction to split.
/// </summary>
public class SplitTransactionPart
{
    /// <summary>
    /// Signed like the transaction negative for money out.
    /// </summary>
    [Description("The part's amount, signed like the transaction")]
    public decimal Amount { get; set; }

    /// <summary>
    /// One of the user's tags, null to leave the part untagged.
    /// </summary>
    [Description("The tag the part counts under, null for none")]
    public Guid? TagId { get; set; }

    /// <summary>
    /// What the part was for.
    /// </summary>
    [Description("What the part was for")]
    public string? Note { get; set; }
}
