using System.Net.Http.Json;
using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.NetWorth;
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

    /// <param name="httpClientFactory">Makes the API's client.</param>
    public SavingsGoalsApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    /// <inheritdoc/>
    public IAsyncEnumerable<SavingsGoal> GetSavingsGoalsAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<SavingsGoal>(ApiRoutes.SavingsGoals, cancellationToken);

    /// <inheritdoc/>
    public async Task<SavingsGoal> GetSavingsGoalAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.GetAsync($"{ApiRoutes.SavingsGoals}/{id}", cancellationToken);
        await response.EnsureFoundAsync(cancellationToken);
        return await response.ReadRequiredJsonAsync<SavingsGoal>(cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<SavingsGoalAccount> GetSavingsAccountsAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<SavingsGoalAccount>($"{ApiRoutes.SavingsGoals}/accounts", cancellationToken);

    /// <inheritdoc/>
    public async Task<List<BalancePoint>> GetSavingsGoalHistoryAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.GetAsync($"{ApiRoutes.SavingsGoals}/{id}/history", cancellationToken);
        await response.EnsureFoundAsync(cancellationToken);
        return await response.ReadRequiredJsonAsync<List<BalancePoint>>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SavingsGoal> AddSavingsGoalAsync(SaveSavingsGoal goal, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PostAsJsonAsync(ApiRoutes.SavingsGoals, goal, cancellationToken);
        await response.EnsureNameFreeAsync(NameTakenException.SavingsGoal, goal.Name, cancellationToken);
        return await response.ReadRequiredJsonAsync<SavingsGoal>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SavingsGoal> UpdateSavingsGoalAsync(Guid id, SaveSavingsGoal goal,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PutAsJsonAsync($"{ApiRoutes.SavingsGoals}/{id}", goal, cancellationToken);
        await response.EnsureNameFreeAsync(NameTakenException.SavingsGoal, goal.Name, cancellationToken);
        return await response.ReadRequiredJsonAsync<SavingsGoal>(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteSavingsGoalAsync(Guid id, CancellationToken cancellationToken) =>
        _http.DeleteIfFoundAsync($"{ApiRoutes.SavingsGoals}/{id}", cancellationToken);
}
