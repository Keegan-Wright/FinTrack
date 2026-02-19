using FinanceTracker.Enums;
using FinanceTracker.Models.Request.Transaction;
using FinanceTracker.Models.Response.Transaction;

namespace FinanceTracker.Services.Transactions;

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