using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// The parts to split a transaction into, replacing any split before.
/// </summary>
public class SplitTransaction
{
    /// <summary>
    /// The most parts a transaction splits into.
    /// </summary>
    public const int MaxParts = 20;

    /// <summary>
    /// The longest a part's note can be.
    /// </summary>
    public const int MaxNoteLength = 200;

    /// <summary>
    /// At least two, each signed like the transaction, adding up to its amount.
    /// </summary>
    [Description("The parts, each signed like the transaction and adding up to its amount")]
    public List<SplitTransactionPart> Parts { get; set; } = [];
}
