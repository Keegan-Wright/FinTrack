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
    /// The bank's account type, e.g. <c>TRANSACTION</c> or <c>SAVINGS</c>.
    /// </summary>
    [Description("Type of the account")]
    public required string AccountType { get; init; }

    /// <summary>
    /// The balance at the last sync, in the account's currency. 0 if the balance hasn't synced.
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
    /// The account's transactions.
    /// </summary>
    [Description("List of transactions for the account")]
    public IReadOnlyList<AccountTransaction>? Transactions { get; init; }
}
