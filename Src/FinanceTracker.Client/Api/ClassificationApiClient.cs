using System.Net.Http.Json;
using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IClassificationService"/> over <see cref="ApiRoutes.Classifications"/>. The API's 404s come back as the
/// <see cref="InvalidOperationException"/> the interface documents, and its 409s as the
/// <see cref="NameTakenException"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<IClassificationService>]
public sealed class ClassificationApiClient : IClassificationService
{
    private readonly HttpClient _http;

    /// <param name="httpClientFactory">Makes the API's client.</param>
    public ClassificationApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    /// <inheritdoc/>
    public IAsyncEnumerable<Classification> GetAllCustomClassificationsAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<Classification>(ApiRoutes.Classifications, cancellationToken);

    /// <inheritdoc/>
    public async Task<GetClassification> GetClassificationAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.GetAsync($"{ApiRoutes.Classifications}/{id}", cancellationToken);
        await response.EnsureFoundAsync(cancellationToken);
        return await response.ReadRequiredJsonAsync<GetClassification>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Classification> AddCustomClassificationAsync(AddClassifications classification,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PostAsJsonAsync(ApiRoutes.Classifications, classification, cancellationToken);
        await response.EnsureNameFreeAsync(NameTakenException.Tag, classification.Tag, cancellationToken);
        return await response.ReadRequiredJsonAsync<Classification>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Classification> RenameCustomClassificationAsync(Guid id, RenameClassification request,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PutAsJsonAsync($"{ApiRoutes.Classifications}/{id}", request, cancellationToken);
        await response.EnsureNameFreeAsync(NameTakenException.Tag, request.Tag, cancellationToken);
        return await response.ReadRequiredJsonAsync<Classification>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AddCustomClassificationsToTransactionAsync(AddCustomClassificationsToTransaction model,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync($"{ApiRoutes.Classifications}/transactions",
            model, cancellationToken);
        await response.EnsureFoundAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RemoveCustomClassificationAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.DeleteAsync($"{ApiRoutes.Classifications}/{id}", cancellationToken);
        await response.EnsureFoundAsync(cancellationToken);
    }
}
