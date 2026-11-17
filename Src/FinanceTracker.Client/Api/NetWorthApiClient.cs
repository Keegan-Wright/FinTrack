using System.Globalization;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.NetWorth;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="INetWorthService"/> over <see cref="ApiRoutes.NetWorth"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<INetWorthService>]
public sealed class NetWorthApiClient : INetWorthService
{
    private readonly HttpClient _http;

    /// <param name="httpClientFactory">Makes the API's client.</param>
    public NetWorthApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    /// <inheritdoc/>
    public async Task<NetWorthHistory> GetNetWorthAsync(DateOnly? from, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync(WithFrom(ApiRoutes.NetWorth, from),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<NetWorthHistory>(cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<AccountBalanceHistory> GetBalanceHistoriesAsync(DateOnly? from,
        CancellationToken cancellationToken) =>
        _http.GetStreamAsync<AccountBalanceHistory>(WithFrom($"{ApiRoutes.NetWorth}/accounts", from),
            cancellationToken);

    /// <summary>
    /// <paramref name="path"/> with the day to start from, when there's one.
    /// </summary>
    /// <param name="path">The API path.</param>
    /// <param name="from">The first day, null for all.</param>
    private static string WithFrom(string path, DateOnly? from)
    {
        if (from is not { } day)
        {
            return path;
        }

        return $"{path}?from={day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
    }
}
