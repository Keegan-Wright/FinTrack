namespace FinanceTracker.Domain.OpenBanking.Transfers;

public enum TransferDecision
{
    /// <summary>
    /// Money moved between the user's own accounts, left out of income and spending.
    /// </summary>
    Transfer = 1,

    /// <summary>
    /// Counts as income or spending even though it may look like a transfer.
    /// </summary>
    NotATransfer = 2,

    /// <summary>
    /// Might be a transfer. Still counted until the user decides.
    /// </summary>
    NeedsReview = 3
}
