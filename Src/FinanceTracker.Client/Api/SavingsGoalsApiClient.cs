using System.Net.Http.Json;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="ISavingsGoalsService"/> over <see cref="ApiRoutes.SavingsGoals"/>. The API's 404s come back as the
/// <see cref="InvalidOperationException"/> the interface documents.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<ISavingsGoalsService>]
public sealed class SavingsGoalsApiClient : ISavingsGoalsService
{
    private readonly HttpClient _http;

    public SavingsGoalsApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public IAsyncEnumerable<SavingsGoal> GetSavingsGoalsAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<SavingsGoal>(ApiRoutes.SavingsGoals, cancellationToken);

    public async Task<SavingsGoal> GetSavingsGoalAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.GetAsync($"{ApiRoutes.SavingsGoals}/{id}", cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<SavingsGoal>(cancellationToken);
    }

    public IAsyncEnumerable<SavingsGoalAccount> GetSavingsAccountsAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<SavingsGoalAccount>($"{ApiRoutes.SavingsGoals}/accounts", cancellationToken);

    public async Task<SavingsGoal> AddSavingsGoalAsync(SaveSavingsGoal goal, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PostAsJsonAsync(ApiRoutes.SavingsGoals, goal, cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<SavingsGoal>(cancellationToken);
    }

    public async Task<SavingsGoal> UpdateSavingsGoalAsync(Guid id, SaveSavingsGoal goal,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PutAsJsonAsync($"{ApiRoutes.SavingsGoals}/{id}", goal, cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<SavingsGoal>(cancellationToken);
    }

    public Task<bool> DeleteSavingsGoalAsync(Guid id, CancellationToken cancellationToken) =>
        _http.DeleteIfFoundAsync($"{ApiRoutes.SavingsGoals}/{id}", cancellationToken);
}
