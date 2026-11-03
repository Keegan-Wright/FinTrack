using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// One part of a split transaction.
/// </summary>
public class TransactionSplitPart
{
    /// <summary>
    /// Signed like the transaction negative for money out.
    /// </summary>
    [Description("The part's amount, signed like the transaction")]
    public required decimal Amount { get; init; }

    /// <summary>
    /// The tag it counts under, null when untagged.
    /// </summary>
    [Description("The tag the part counts under, null for none")]
    public Guid? TagId { get; init; }

    /// <summary>
    /// That tag's name.
    /// </summary>
    [Description("The name of the tag the part counts under")]
    public string? Tag { get; init; }

    /// <summary>
    /// What the part was for.
    /// </summary>
    [Description("What the part was for")]
    public string? Note { get; init; }
}
