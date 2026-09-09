using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Infrastructure.Transactions;

public interface ITransactionsService
{
    IAsyncEnumerable<Transaction> GetAllTransactionsAsync(
        FilterTransactions filterTransactions, SyncTypes syncTypes,
        CancellationToken cancellationToken);

    IAsyncEnumerable<TransactionAccountFilter> GetAccountsForTransactionFiltersAsync(SyncTypes syncTypes,
        CancellationToken cancellationToken);

    IAsyncEnumerable<TransactionProviderFilter> GetProvidersForTransactionFiltersAsync(
        CancellationToken cancellationToken);

    IAsyncEnumerable<TransactionTypeFilter> GetTypesForTransactionFiltersAsync(
        CancellationToken cancellationToken);

    IAsyncEnumerable<TransactionCategoryFilter> GetCategoriesForTransactionFiltersAsync(
        CancellationToken cancellationToken);

    IAsyncEnumerable<TransactionTagFilter> GetTagsForTransactionFiltersAsync(
        CancellationToken cancellationToken);
}
