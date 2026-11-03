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

    public NetWorthApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public async Task<NetWorthHistory> GetNetWorthAsync(DateOnly? from, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync(WithFrom(ApiRoutes.NetWorth, from),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<NetWorthHistory>(cancellationToken);
    }

    public IAsyncEnumerable<AccountBalanceHistory> GetBalanceHistoriesAsync(DateOnly? from,
        CancellationToken cancellationToken) =>
        _http.GetStreamAsync<AccountBalanceHistory>(WithFrom($"{ApiRoutes.NetWorth}/accounts", from),
            cancellationToken);

    private static string WithFrom(string path, DateOnly? from)
    {
        if (from is not { } day)
        {
            return path;
        }

        return $"{path}?from={day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
    }
}
