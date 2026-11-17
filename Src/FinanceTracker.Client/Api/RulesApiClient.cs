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

    /// <param name="httpClientFactory">Makes the API's client.</param>
    public RulesApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    /// <inheritdoc/>
    public IAsyncEnumerable<Rule> GetRulesAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<Rule>(ApiRoutes.Rules, cancellationToken);

    /// <inheritdoc/>
    public async Task<Rule> GetRuleAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync($"{ApiRoutes.Rules}/{id}", cancellationToken);
        await response.EnsureFoundAsync(cancellationToken);
        return await response.ReadRequiredJsonAsync<Rule>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Rule> AddRuleAsync(SaveRule rule, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(ApiRoutes.Rules, rule, cancellationToken);
        await response.EnsureFoundAsync(cancellationToken);
        return await response.ReadRequiredJsonAsync<Rule>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Rule> UpdateRuleAsync(Guid id, SaveRule rule, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PutAsJsonAsync($"{ApiRoutes.Rules}/{id}", rule, cancellationToken);
        await response.EnsureFoundAsync(cancellationToken);
        return await response.ReadRequiredJsonAsync<Rule>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ReorderRulesAsync(ReorderRules order, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PutAsJsonAsync($"{ApiRoutes.Rules}/order", order, cancellationToken);
        await response.EnsureFoundAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<RulePreview> PreviewRuleAsync(Guid? id, SaveRule rule, CancellationToken cancellationToken)
    {
        string path = id is { } ruleId ? $"{ApiRoutes.Rules}/{ruleId}/preview" : $"{ApiRoutes.Rules}/preview";
        using HttpResponseMessage response = await _http.PostAsJsonAsync(path, rule, cancellationToken);
        await response.EnsureFoundAsync(cancellationToken);
        return await response.ReadRequiredJsonAsync<RulePreview>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> ApplyRuleToExistingAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PostAsync($"{ApiRoutes.Rules}/{id}/apply", null, cancellationToken);
        await response.EnsureFoundAsync(cancellationToken);
        return await response.ReadRequiredJsonAsync<int>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SaveRule> GetDraftFromTransactionAsync(Guid transactionId,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.GetAsync($"{ApiRoutes.Rules}/draft/{transactionId}", cancellationToken);
        await response.EnsureFoundAsync(cancellationToken);
        return await response.ReadRequiredJsonAsync<SaveRule>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteRuleAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.DeleteAsync($"{ApiRoutes.Rules}/{id}", cancellationToken);
        await response.EnsureFoundAsync(cancellationToken);
    }
}
