using System.Net.Http.Json;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IClassificationService"/> over <see cref="ApiRoutes.Classifications"/>. The API's 404s come back as the
/// <see cref="InvalidOperationException"/> the interface documents.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<IClassificationService>]
public sealed class ClassificationApiClient : IClassificationService
{
    private readonly HttpClient _http;

    public ClassificationApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public IAsyncEnumerable<Classification> GetAllCustomClassificationsAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<Classification>(ApiRoutes.Classifications, cancellationToken);

    public async Task<GetClassification> GetClassificationAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.GetAsync($"{ApiRoutes.Classifications}/{id}", cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<GetClassification>(cancellationToken);
    }

    public Task<Classification> AddCustomClassificationAsync(AddClassifications classification,
        CancellationToken cancellationToken) =>
        _http.PostReadAsync<AddClassifications, Classification>(ApiRoutes.Classifications, classification,
            cancellationToken);

    public async Task AddCustomClassificationsToTransactionAsync(AddCustomClassificationsToTransaction model,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync($"{ApiRoutes.Classifications}/transactions",
            model, cancellationToken);
        response.EnsureFound();
    }

    public async Task RemoveCustomClassificationAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.DeleteAsync($"{ApiRoutes.Classifications}/{id}", cancellationToken);
        response.EnsureFound();
    }
}
