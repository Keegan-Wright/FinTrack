namespace FinanceTracker.Domain.Sharing;

/// <summary>
/// What kind of thing a <see cref="Share"/> shares. More kinds follow as each feature learns to share.
/// </summary>
public enum ShareItemType
{
    /// <summary>
    /// A bank account or card, with its transactions, balances and history.
    /// </summary>
    BankAccount = 0,

    /// <summary>
    /// A custom classification, a tag. Chosen, the other person can use it on their own transactions too, and rename
    /// it if allowed. Or read only, when it's put on the other person's transaction so they see its name.
    /// </summary>
    Classification = 1,

    /// <summary>
    /// A bill, read only, shared when the other person's transaction is said to pay it so they see its name.
    /// </summary>
    Bill = 2,

    /// <summary>
    /// A debt, read only, shared when the other person's transaction is said to pay it off so they see its name.
    /// </summary>
    Debt = 3
}
