using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="ISavingsGoalsService"/> over <see cref="ApiRoutes.SavingsGoals"/>.
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

    public Task<SavingsGoal> AddSavingsGoalAsync(AddSavingsGoal goalToAdd,
        CancellationToken cancellationToken) =>
        _http.PostReadAsync<AddSavingsGoal, SavingsGoal>(ApiRoutes.SavingsGoals, goalToAdd,
            cancellationToken);

    public Task<bool> DeleteSavingsGoalAsync(Guid id, CancellationToken cancellationToken) =>
        _http.DeleteIfFoundAsync($"{ApiRoutes.SavingsGoals}/{id}", cancellationToken);
}
