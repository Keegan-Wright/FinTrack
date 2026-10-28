using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="ITransfersService"/> over <see cref="ApiRoutes.Transfers"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<ITransfersService>]
public sealed class TransfersApiClient : ITransfersService
{
    private readonly HttpClient _http;

    public TransfersApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public IAsyncEnumerable<Transaction> GetTransfersToReviewAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<Transaction>($"{ApiRoutes.Transfers}/review", cancellationToken);

    public async Task<int> GetReviewCountAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.GetAsync($"{ApiRoutes.Transfers}/review/count", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<int>(cancellationToken);
    }

    public Task<IReadOnlyList<Transaction>> MarkAsTransferAsync(Guid id, CancellationToken cancellationToken) =>
        DecideAsync(HttpMethod.Post, $"{ApiRoutes.Transfers}/{id}/transfer", cancellationToken);

    public Task<IReadOnlyList<Transaction>> MarkNotATransferAsync(Guid id, CancellationToken cancellationToken) =>
        DecideAsync(HttpMethod.Post, $"{ApiRoutes.Transfers}/{id}/not-a-transfer", cancellationToken);

    public Task<IReadOnlyList<Transaction>> ResetTransferAsync(Guid id, CancellationToken cancellationToken) =>
        DecideAsync(HttpMethod.Delete, $"{ApiRoutes.Transfers}/{id}/decision", cancellationToken);

    private async Task<IReadOnlyList<Transaction>> DecideAsync(HttpMethod method, string path,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(method, path);
        using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<List<Transaction>>(cancellationToken);
    }
}
