using System.Collections.Immutable;
using System.ComponentModel;

namespace FinanceTracker.Models.Response.Account;

public class AccountAndTransactionsResponse
{
    [Description("Name of the account")]
    public required string AccountName { get; init; }

    [Description("Account logo in byte array format")]
    public required byte[] Logo { get; init; }

    [Description("Type of the account")]
    public required string AccountType { get; init; }

    [Description("Current balance of the account")]
    public required decimal AccountBalance { get; init; }

    [Description("Available balance in the account")]
    public required decimal AvailableBalance { get; init; }

    [Description("List of transactions for the account")]
    public IImmutableList<AccountTransactionResponse>? Transactions { get; init; }
}
