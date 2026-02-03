using System.ComponentModel;

namespace FinanceTracker.Models.Response.Account;

public class AccountAndTransactionsResponse
{
    [Description("Name of the account")]
    public string AccountName { get; set; }

    [Description("Account logo in byte array format")]
    public byte[] Logo { get; set; }

    [Description("Type of the account")]
    public string AccountType { get; set; }

    [Description("Current balance of the account")]
    public decimal AccountBalance { get; set; }

    [Description("Available balance in the account")]
    public decimal AvailableBalance { get; set; }

    [Description("List of transactions for the account")]
    public IAsyncEnumerable<AccountTransactionResponse>? Transactions { get; set; }
}