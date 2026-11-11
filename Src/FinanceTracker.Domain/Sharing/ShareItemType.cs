namespace FinanceTracker.Domain.Sharing;

/// <summary>
/// What kind of thing a <see cref="Share"/> shares. More kinds follow as each feature learns to share.
/// </summary>
public enum ShareItemType
{
    /// <summary>
    /// A bank account or card, with its transactions, balances and history.
    /// </summary>
    BankAccount = 0
}
