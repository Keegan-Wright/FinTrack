namespace FinanceTracker.Domain.OpenBanking.Accounts;

/// <summary>
/// Whether a <see cref="BankAccount"/> is an account or a card, which decides where its data is fetched from and how
/// its balance counts.
/// </summary>
public enum BankAccountKind
{
    /// <summary>
    /// A current, savings or similar account. Its balance is money held.
    /// </summary>
    Account,

    /// <summary>
    /// A credit or charge card. Its balance is stored as what's owed, as a negative amount.
    /// </summary>
    Card
}
