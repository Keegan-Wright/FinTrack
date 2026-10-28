namespace FinanceTracker.Client.Components.Transactions;

/// <summary>
/// What the user can decide about whether a transaction is a transfer, see <see cref="TransferChip"/>.
/// </summary>
public enum TransferAction
{
    /// <summary>
    /// It's a transfer, confirming a suggested other half with it.
    /// </summary>
    MarkAsTransfer,

    /// <summary>
    /// It counts as income or spending.
    /// </summary>
    NotATransfer,

    /// <summary>
    /// Drop the user's decision so FinTrack decides again.
    /// </summary>
    LetFinTrackDecide
}
