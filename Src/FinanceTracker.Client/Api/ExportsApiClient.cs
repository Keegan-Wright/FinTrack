using System.Net.Http.Json;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IExportsService"/> over <see cref="ApiRoutes.Exports"/>, copying the file the API streams.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<IExportsService>]
public sealed class ExportsApiClient : IExportsService
{
    private readonly HttpClient _http;

    public ExportsApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public async Task WriteTransactionsCsvAsync(FilterTransactions filter, Stream destination,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        using HttpResponseMessage response = await _http.PostAsJsonAsync($"{ApiRoutes.Exports}/transactions", filter,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await response.Content.CopyToAsync(destination, cancellationToken);
    }

    public async Task WriteBackupAsync(Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        using HttpResponseMessage response = await _http.GetAsync($"{ApiRoutes.Exports}/backup",
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await response.Content.CopyToAsync(destination, cancellationToken);
    }
}
