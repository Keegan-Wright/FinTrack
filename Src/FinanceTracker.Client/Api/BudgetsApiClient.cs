using System.Net.Http.Json;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IBudgetsService"/> over <see cref="ApiRoutes.Budgets"/>. The API's 404s come back as the
/// <see cref="InvalidOperationException"/> the interface documents.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<IBudgetsService>]
public sealed class BudgetsApiClient : IBudgetsService
{
    private readonly HttpClient _http;

    /// <param name="httpClientFactory">Makes the API's client.</param>
    public BudgetsApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    /// <inheritdoc/>
    public async Task<BudgetMonthOverview> GetMonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.GetAsync($"{ApiRoutes.Budgets}/month/{year}/{month}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<BudgetMonthOverview>(cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<Budget> GetBudgetsAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<Budget>(ApiRoutes.Budgets, cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<BudgetAccount> GetAccountsAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<BudgetAccount>($"{ApiRoutes.Budgets}/accounts", cancellationToken);

    /// <inheritdoc/>
    public async Task<Budget> GetBudgetAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync($"{ApiRoutes.Budgets}/{id}", cancellationToken);
        await response.EnsureFoundAsync(cancellationToken);
        return await response.ReadRequiredJsonAsync<Budget>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Budget> AddBudgetAsync(SaveBudget budget, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(ApiRoutes.Budgets, budget, cancellationToken);
        await response.EnsureFoundAsync(cancellationToken);
        return await response.ReadRequiredJsonAsync<Budget>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Budget> UpdateBudgetAsync(Guid id, SaveBudget budget, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PutAsJsonAsync($"{ApiRoutes.Budgets}/{id}", budget, cancellationToken);
        await response.EnsureFoundAsync(cancellationToken);
        return await response.ReadRequiredJsonAsync<Budget>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteBudgetAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.DeleteAsync($"{ApiRoutes.Budgets}/{id}", cancellationToken);
        await response.EnsureFoundAsync(cancellationToken);
    }
}
