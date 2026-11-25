using System.ComponentModel;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Ai.Tools.Lookups;

/// <summary>
/// Tools that list what the other tools can filter by.
/// </summary>
/// <param name="transactionsService">Reads the user's accounts and tags.</param>
public sealed class LookupTools(ITransactionsService transactionsService)
{
    /// <summary>
    /// The user's accounts.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    [AiTool("list_accounts")]
    [Description("Lists the user's accounts with their ids. Use it to turn an account name into an id for the " +
        "other tools.")]
    public async Task<List<AccountFound>> ListAccountsAsync(CancellationToken cancellationToken)
    {
        List<AccountFound> accounts = [];
        await foreach (TransactionAccountFilter account in transactionsService
            .GetAccountsForTransactionFiltersAsync(cancellationToken))
        {
            accounts.Add(new AccountFound(account.AccountId, account.AccountName));
        }

        return accounts;
    }

    /// <summary>
    /// The tags on the user's transactions.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    [AiTool("list_tags")]
    [Description("Lists the tags on the user's transactions, such as Groceries or Takeaways. Tags are how " +
        "spending is grouped, use these names in the other tools' tag filters.")]
    public async Task<List<string>> ListTagsAsync(CancellationToken cancellationToken)
    {
        List<string> tags = [];
        await foreach (TransactionTagFilter tag in transactionsService
            .GetTagsForTransactionFiltersAsync(cancellationToken))
        {
            tags.Add(tag.Tag);
        }

        return tags;
    }
}
