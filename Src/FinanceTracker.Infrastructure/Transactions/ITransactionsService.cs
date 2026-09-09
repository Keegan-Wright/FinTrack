using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Transaction;

namespace FinanceTracker.Infrastructure.Transactions;

public interface ITransactionsService
{
    IAsyncEnumerable<TransactionResponse> GetAllTransactionsAsync(
        FilteredTransactionsRequest filteredTransactionsRequest, SyncTypes syncTypes,
        CancellationToken cancellationToken);

    IAsyncEnumerable<TransactionAccountFilterResponse> GetAccountsForTransactionFiltersAsync(SyncTypes syncTypes,
        CancellationToken cancellationToken);

    IAsyncEnumerable<TransactionProviderFilterResponse> GetProvidersForTransactionFiltersAsync(
        CancellationToken cancellationToken);

    IAsyncEnumerable<TransactionTypeFilterResponse> GetTypesForTransactionFiltersAsync(
        CancellationToken cancellationToken);

    IAsyncEnumerable<TransactionCategoryFilterResponse> GetCategoriesForTransactionFiltersAsync(
        CancellationToken cancellationToken);

    IAsyncEnumerable<TransactionTagFilterResponse> GetTagsForTransactionFiltersAsync(
        CancellationToken cancellationToken);
}
