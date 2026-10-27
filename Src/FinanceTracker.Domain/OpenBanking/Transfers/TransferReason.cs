namespace FinanceTracker.Domain.OpenBanking.Transfers;

/// <summary>
/// Why the <see cref="TransferMatcher"/> decided what it did.
/// </summary>
public enum TransferReason
{
    /// <summary>
    /// The bank's reference names another of the user's accounts.
    /// </summary>
    OwnAccountReference = 1,

    /// <summary>
    /// An equal and opposite amount on another of the user's accounts within a couple of days.
    /// </summary>
    MirrorPair = 2,

    /// <summary>
    /// Several equally good mirror pairs, so none was picked.
    /// </summary>
    AmbiguousMirrorPair = 3,

    /// <summary>
    /// The bank labels it a transfer but no other half was found.
    /// </summary>
    BankTransferLabel = 4
}
