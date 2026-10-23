using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="ITransactionsService"/> over <see cref="ApiRoutes.Transactions"/>.
/// </summary>
public sealed class TransactionsApiClient : ITransactionsService
{
    private readonly HttpClient _http;

    public TransactionsApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public IAsyncEnumerable<Transaction> GetAllTransactionsAsync(FilterTransactions filterTransactions,
        CancellationToken cancellationToken) =>
        _http.PostStreamAsync<FilterTransactions, Transaction>($"{ApiRoutes.Transactions}/search",
            filterTransactions, cancellationToken);

    public IAsyncEnumerable<TransactionAccountFilter> GetAccountsForTransactionFiltersAsync(
        CancellationToken cancellationToken) =>
        _http.GetStreamAsync<TransactionAccountFilter>(Filter("accounts"), cancellationToken);

    public IAsyncEnumerable<TransactionProviderFilter> GetProvidersForTransactionFiltersAsync(
        CancellationToken cancellationToken) =>
        _http.GetStreamAsync<TransactionProviderFilter>(Filter("providers"), cancellationToken);

    public IAsyncEnumerable<TransactionTypeFilter> GetTypesForTransactionFiltersAsync(
        CancellationToken cancellationToken) =>
        _http.GetStreamAsync<TransactionTypeFilter>(Filter("types"), cancellationToken);

    public IAsyncEnumerable<TransactionCategoryFilter> GetCategoriesForTransactionFiltersAsync(
        CancellationToken cancellationToken) =>
        _http.GetStreamAsync<TransactionCategoryFilter>(Filter("categories"), cancellationToken);

    public IAsyncEnumerable<TransactionTagFilter> GetTagsForTransactionFiltersAsync(
        CancellationToken cancellationToken) =>
        _http.GetStreamAsync<TransactionTagFilter>(Filter("tags"), cancellationToken);

    private static string Filter(string name) => $"{ApiRoutes.Transactions}/filters/{name}";
}
