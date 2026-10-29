using System.ComponentModel;

namespace FinanceTracker.Contracts.Account;

/// <summary>
/// One of the user's bank accounts with its latest balance and newest transactions.
/// </summary>
public class AccountAndTransactions
{
    /// <summary>
    /// The account's name as the bank reports it.
    /// </summary>
    [Description("Name of the account")]
    public required string AccountName { get; init; }

    /// <summary>
    /// The bank's logo, an SVG image.
    /// </summary>
    [Description("Account logo in byte array format")]
    public required byte[] Logo { get; init; }

    /// <summary>
    /// The bank's account type, e.g. <c>TRANSACTION</c> or <c>SAVINGS</c>, or <c>CREDIT_CARD</c> for a card.
    /// </summary>
    [Description("Type of the account")]
    public required string AccountType { get; init; }

    /// <summary>
    /// The balance at the last sync, in the account's currency. 0 if the balance hasn't synced. For a card, what's
    /// owed as a negative amount.
    /// </summary>
    [Description("Current balance of the account")]
    public required decimal AccountBalance { get; init; }

    /// <summary>
    /// What can be spent at the last sync, in the account's currency. 0 if the balance
    /// hasn't synced.
    /// </summary>
    [Description("Available balance in the account")]
    public required decimal AvailableBalance { get; init; }

    /// <summary>
    /// A card's last digits, null for an account or when the bank doesn't say.
    /// </summary>
    [Description("Last digits of the card number, for cards")]
    public string? CardEnding { get; init; }

    /// <summary>
    /// A card's credit limit at the last sync, null for an account or when the bank doesn't say.
    /// </summary>
    [Description("Credit limit, for cards")]
    public decimal? CreditLimit { get; init; }

    /// <summary>
    /// The account's transactions.
    /// </summary>
    [Description("List of transactions for the account")]
    public IReadOnlyList<AccountTransaction>? Transactions { get; init; }
}
