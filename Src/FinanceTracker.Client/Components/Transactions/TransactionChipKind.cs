namespace FinanceTracker.Client.Components.Transactions;

/// <summary>
/// What a <see cref="TransactionChip"/> stands for, which decides how it looks.
/// </summary>
public enum TransactionChipKind
{
    /// <summary>
    /// A bill, subscription, debt or savings goal it pays or counts towards, linking to it.
    /// </summary>
    Link,

    /// <summary>
    /// One of the user's own tags.
    /// </summary>
    Tag,

    /// <summary>
    /// One of the bank's classifications, shown quieter than the user's own.
    /// </summary>
    Bank
}
