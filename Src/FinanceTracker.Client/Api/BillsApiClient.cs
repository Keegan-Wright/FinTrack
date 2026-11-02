using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IBillsService"/> over <see cref="ApiRoutes.Bills"/>. The API's 404s come back as the
/// <see cref="InvalidOperationException"/> the interface documents, and its 409 for deleting a bill that can only be
/// ended as the <see cref="NotSupportedException"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<IBillsService>]
public sealed class BillsApiClient : IBillsService
{
    private readonly HttpClient _http;

    public BillsApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public async Task<BillMonthOverview> GetMonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.GetAsync($"{ApiRoutes.Bills}/month/{year}/{month}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<BillMonthOverview>(cancellationToken);
    }

    public IAsyncEnumerable<Bill> GetBillsAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<Bill>(ApiRoutes.Bills, cancellationToken);

    public async Task<Bill> GetBillAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync($"{ApiRoutes.Bills}/{id}", cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<Bill>(cancellationToken);
    }

    public async Task<Bill> AddBillAsync(SaveBill bill, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(ApiRoutes.Bills, bill, cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<Bill>(cancellationToken);
    }

    public async Task<Bill> UpdateBillAsync(Guid id, SaveBill bill, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PutAsJsonAsync($"{ApiRoutes.Bills}/{id}", bill, cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<Bill>(cancellationToken);
    }

    public Task<Bill> EndBillAsync(Guid id, CancellationToken cancellationToken) =>
        PostForBillAsync($"{ApiRoutes.Bills}/{id}/end", cancellationToken);

    public Task<Bill> ReopenBillAsync(Guid id, CancellationToken cancellationToken) =>
        PostForBillAsync($"{ApiRoutes.Bills}/{id}/reopen", cancellationToken);

    public async Task DeleteBillAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.DeleteAsync($"{ApiRoutes.Bills}/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new NotSupportedException("A bill from a standing order or direct debit can only be ended.");
        }

        response.EnsureFound();
    }

    public async Task<Transaction> PayBillAsync(Guid transactionId, PayBill payment,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PutAsJsonAsync($"{ApiRoutes.Bills}/payments/{transactionId}", payment, cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<Transaction>(cancellationToken);
    }

    public IAsyncEnumerable<BillSuggestion> GetSuggestionsAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<BillSuggestion>($"{ApiRoutes.Bills}/suggestions", cancellationToken);

    public async Task<int> GetSuggestionCountAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.GetAsync($"{ApiRoutes.Bills}/suggestions/count", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<int>(cancellationToken);
    }

    public async Task<Bill> ConfirmSuggestionAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PostAsync($"{ApiRoutes.Bills}/suggestions/{id}/confirm", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new AlreadyABillException(await CoveringBillNameAsync(response, cancellationToken));
        }

        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<Bill>(cancellationToken);
    }

    /// <summary>
    /// The bill a 409 for confirming a suggestion names, see <see cref="AlreadyABillException.BillNameField"/>.
    /// </summary>
    private static async Task<string> CoveringBillNameAsync(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        using JsonDocument problem = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken)
            ?? throw new InvalidOperationException("The API's 409 had no body.");

        if (problem.RootElement.TryGetProperty(AlreadyABillException.BillNameField, out JsonElement billName) &&
            billName.GetString() is { } name)
        {
            return name;
        }

        return "another bill";
    }

    public async Task DismissSuggestionAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PostAsync($"{ApiRoutes.Bills}/suggestions/{id}/dismiss", null, cancellationToken);
        response.EnsureFound();
    }

    private async Task<Bill> PostForBillAsync(string path, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PostAsync(path, null, cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<Bill>(cancellationToken);
    }
}
