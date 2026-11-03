namespace FinanceTracker.Client.Components.Transactions;

/// <summary>
/// One part of a split as the user fills it in, see <see cref="SplitTransactionDialog"/>.
/// </summary>
public sealed class SplitPartDraft
{
    /// <summary>
    /// How much, always entered as money, whichever way the transaction went.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// The tag it counts under, <see cref="Guid.Empty"/> for none.
    /// </summary>
    public Guid TagId { get; set; }

    public string? Note { get; set; }
}
