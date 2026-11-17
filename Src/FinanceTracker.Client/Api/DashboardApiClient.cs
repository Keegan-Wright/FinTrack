using System.Globalization;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IDashboardService"/> over <see cref="ApiRoutes.Dashboard"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<IDashboardService>]
public sealed class DashboardApiClient : IDashboardService
{
    private readonly HttpClient _http;

    /// <param name="httpClientFactory">Makes the API's client.</param>
    public DashboardApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    /// <inheritdoc/>
    public async Task<SpentInTimePeriod> GetSpentInTimePeriod(DateTime fromDate, DateTime toDate,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync(
            $"{ApiRoutes.Dashboard}/spent?from={QueryTime(fromDate)}&to={QueryTime(toDate)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<SpentInTimePeriod>(cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<UpcomingPayment> GetUpcomingPaymentsAsync(int numberToFetch,
        CancellationToken cancellationToken) =>
        _http.GetStreamAsync<UpcomingPayment>($"{ApiRoutes.Dashboard}/upcoming-payments?count={numberToFetch}",
            cancellationToken);

    // As UTC, so the instant survives the server being in another time zone
    /// <summary>
    /// <paramref name="time"/> in UTC, for a query string.
    /// </summary>
    /// <param name="time">The time.</param>
    private static string QueryTime(DateTime time) =>
        Uri.EscapeDataString(time.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
}
