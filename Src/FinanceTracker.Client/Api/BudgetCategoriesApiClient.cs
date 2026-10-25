using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Budget;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IBudgetCategoriesService"/> over <see cref="ApiRoutes.BudgetCategories"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.ApiClient)]
[Scoped<IBudgetCategoriesService>]
public sealed class BudgetCategoriesApiClient : IBudgetCategoriesService
{
    private readonly HttpClient _http;

    public BudgetCategoriesApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public IAsyncEnumerable<BudgetCategory> GetBudgetItemsAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<BudgetCategory>(ApiRoutes.BudgetCategories, cancellationToken);

    public Task<BudgetCategory> AddBudgetCategoryAsync(AddBudgetCategory categoryToAdd,
        CancellationToken cancellationToken) =>
        _http.PostReadAsync<AddBudgetCategory, BudgetCategory>(ApiRoutes.BudgetCategories, categoryToAdd,
            cancellationToken);

    public Task<bool> DeleteBudgetCategoryAsync(Guid id, CancellationToken cancellationToken) =>
        _http.DeleteIfFoundAsync($"{ApiRoutes.BudgetCategories}/{id}", cancellationToken);
}
