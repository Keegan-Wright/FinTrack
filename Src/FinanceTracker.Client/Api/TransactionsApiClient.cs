using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="ITransactionsService"/> over <see cref="ApiRoutes.Transactions"/>. The API's 404s come back as the
/// <see cref="InvalidOperationException"/> the interface documents, and its 409 for a split that can't be made as
/// <see cref="CannotSplitTransactionException"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<ITransactionsService>]
public sealed class TransactionsApiClient : ITransactionsService
{
    private readonly HttpClient _http;

    /// <param name="httpClientFactory">Makes the API's client.</param>
    public TransactionsApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    /// <inheritdoc/>
    public IAsyncEnumerable<Transaction> GetAllTransactionsAsync(FilterTransactions filterTransactions,
        CancellationToken cancellationToken) =>
        _http.PostStreamAsync<FilterTransactions, Transaction>($"{ApiRoutes.Transactions}/search",
            filterTransactions, cancellationToken);

    /// <inheritdoc/>
    public Task<Transaction> HideBankClassificationAsync(Guid transactionId, ChangeBankClassification request,
        CancellationToken cancellationToken) =>
        ChangeBankClassificationAsync(transactionId, "hide", request, cancellationToken);

    /// <inheritdoc/>
    public Task<Transaction> ShowBankClassificationAsync(Guid transactionId, ChangeBankClassification request,
        CancellationToken cancellationToken) =>
        ChangeBankClassificationAsync(transactionId, "show", request, cancellationToken);

    /// <summary>
    /// Hides or shows one of the bank's classifications on the transaction.
    /// </summary>
    /// <param name="transactionId">The transaction.</param>
    /// <param name="change">The path for the change, e.g. <c>hide</c>.</param>
    /// <param name="request">Which classification.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    private async Task<Transaction> ChangeBankClassificationAsync(Guid transactionId, string change,
        ChangeBankClassification request, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(
            $"{ApiRoutes.Transactions}/{transactionId}/bank-classifications/{change}", request, cancellationToken);
        await response.EnsureFoundAsync(cancellationToken);
        return await response.ReadRequiredJsonAsync<Transaction>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Transaction> SplitAsync(Guid transactionId, SplitTransaction request,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PutAsJsonAsync(Splits(transactionId), request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new CannotSplitTransactionException(await ReasonAsync(response, cancellationToken));
        }

        await response.EnsureFoundAsync(cancellationToken);
        return await response.ReadRequiredJsonAsync<Transaction>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Transaction> UnsplitAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.DeleteAsync(Splits(transactionId), cancellationToken);
        await response.EnsureFoundAsync(cancellationToken);
        return await response.ReadRequiredJsonAsync<Transaction>(cancellationToken);
    }

    /// <summary>
    /// The transaction's splits.
    /// </summary>
    /// <param name="transactionId">The transaction.</param>
    private static string Splits(Guid transactionId) => $"{ApiRoutes.Transactions}/{transactionId}/splits";

    /// <summary>
    /// The problem's detail, which says why the split can't be made.
    /// </summary>
    /// <param name="response">The API's answer.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    private static async Task<string> ReasonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using JsonDocument? problem = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        if (problem is not null && problem.RootElement.TryGetProperty("detail", out JsonElement detail) &&
            detail.GetString() is { } reason)
        {
            return reason;
        }

        return "The transaction can't be split that way.";
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<TransactionAccountFilter> GetAccountsForTransactionFiltersAsync(
        CancellationToken cancellationToken) =>
        _http.GetStreamAsync<TransactionAccountFilter>(Filter("accounts"), cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<TransactionProviderFilter> GetProvidersForTransactionFiltersAsync(
        CancellationToken cancellationToken) =>
        _http.GetStreamAsync<TransactionProviderFilter>(Filter("providers"), cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<TransactionTypeFilter> GetTypesForTransactionFiltersAsync(
        CancellationToken cancellationToken) =>
        _http.GetStreamAsync<TransactionTypeFilter>(Filter("types"), cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<TransactionCategoryFilter> GetCategoriesForTransactionFiltersAsync(
        CancellationToken cancellationToken) =>
        _http.GetStreamAsync<TransactionCategoryFilter>(Filter("categories"), cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<TransactionTagFilter> GetTagsForTransactionFiltersAsync(
        CancellationToken cancellationToken) =>
        _http.GetStreamAsync<TransactionTagFilter>(Filter("tags"), cancellationToken);

    /// <summary>
    /// One of the filter lists.
    /// </summary>
    /// <param name="name">The list, e.g. <c>accounts</c>.</param>
    private static string Filter(string name) => $"{ApiRoutes.Transactions}/filters/{name}";
}
