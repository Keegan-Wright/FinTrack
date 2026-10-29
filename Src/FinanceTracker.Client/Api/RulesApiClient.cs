using System.Net.Http.Json;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Rules;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IRulesService"/> over <see cref="ApiRoutes.Rules"/>. The API's 404s come back as the
/// <see cref="InvalidOperationException"/> the interface documents.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<IRulesService>]
public sealed class RulesApiClient : IRulesService
{
    private readonly HttpClient _http;

    public RulesApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public IAsyncEnumerable<Rule> GetRulesAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<Rule>(ApiRoutes.Rules, cancellationToken);

    public async Task<Rule> GetRuleAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync($"{ApiRoutes.Rules}/{id}", cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<Rule>(cancellationToken);
    }

    public async Task<Rule> AddRuleAsync(SaveRule rule, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(ApiRoutes.Rules, rule, cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<Rule>(cancellationToken);
    }

    public async Task<Rule> UpdateRuleAsync(Guid id, SaveRule rule, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PutAsJsonAsync($"{ApiRoutes.Rules}/{id}", rule, cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<Rule>(cancellationToken);
    }

    public async Task ReorderRulesAsync(ReorderRules order, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PutAsJsonAsync($"{ApiRoutes.Rules}/order", order, cancellationToken);
        response.EnsureFound();
    }

    public async Task DeleteRuleAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.DeleteAsync($"{ApiRoutes.Rules}/{id}", cancellationToken);
        response.EnsureFound();
    }
}
