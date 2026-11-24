using System.Net.Http.Json;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Exports;
using FinanceTracker.Contracts.PayPeriods;
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

    /// <param name="httpClientFactory">Makes the API's client.</param>
    public ExportsApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    /// <inheritdoc/>
    public async Task WriteTransactionsCsvAsync(FilterTransactions filter, Stream destination,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        using HttpResponseMessage response = await _http.PostAsJsonAsync($"{ApiRoutes.Exports}/transactions", filter,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await response.Content.CopyToAsync(destination, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task WriteBackupAsync(ExportBackup backup, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        using HttpRequestMessage request = new(HttpMethod.Post, $"{ApiRoutes.Exports}/backup")
        {
            Content = JsonContent.Create(backup)
        };

        using HttpResponseMessage response =
            await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();
        await response.Content.CopyToAsync(destination, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<PayPeriod>> GetStatementPeriodsAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.GetAsync($"{ApiRoutes.Exports}/statement/periods", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<List<PayPeriod>>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task WriteStatementAsync(int year, int month, Stream destination,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        using HttpResponseMessage response = await _http.GetAsync($"{ApiRoutes.Exports}/statement/{year}/{month}",
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await response.Content.CopyToAsync(destination, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task WriteReportAsync(ExportReport report, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        using HttpResponseMessage response = await _http.PostAsJsonAsync($"{ApiRoutes.Exports}/report", report,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await response.Content.CopyToAsync(destination, cancellationToken);
    }
}
