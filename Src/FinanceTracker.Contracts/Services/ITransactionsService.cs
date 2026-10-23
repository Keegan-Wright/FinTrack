using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's transactions and the values they can be filtered by.
/// </summary>
/// <remarks>
/// Most transaction columns are encrypted, so filtering beyond account and provider happens after decryption.
/// </remarks>
public interface ITransactionsService
{
    /// <summary>
    /// The user's transactions matching <paramref name="filterTransactions"/>, newest first.
    /// </summary>
    /// <param name="filterTransactions">The filters, empty ones match everything.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    IAsyncEnumerable<Transaction> GetAllTransactionsAsync(FilterTransactions filterTransactions,
        CancellationToken cancellationToken);

    /// <summary>
    /// The user's accounts, for the account filter.
    /// </summary>
    IAsyncEnumerable<TransactionAccountFilter> GetAccountsForTransactionFiltersAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// The user's bank connections, for the provider filter.
    /// </summary>
    IAsyncEnumerable<TransactionProviderFilter> GetProvidersForTransactionFiltersAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Each transaction type found in the user's transactions, once.
    /// </summary>
    IAsyncEnumerable<TransactionTypeFilter> GetTypesForTransactionFiltersAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Each provider category found in the user's transactions, once.
    /// </summary>
    IAsyncEnumerable<TransactionCategoryFilter> GetCategoriesForTransactionFiltersAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Each tag the user can filter by, once: provider classifications found on their transactions and all their
    /// custom classifications.
    /// </summary>
    IAsyncEnumerable<TransactionTagFilter> GetTagsForTransactionFiltersAsync(
        CancellationToken cancellationToken);
}
