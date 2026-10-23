using System.Globalization;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Contracts.Services;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IDashboardService"/> over <see cref="ApiRoutes.Dashboard"/>.
/// </summary>
public sealed class DashboardApiClient : IDashboardService
{
    private readonly HttpClient _http;

    public DashboardApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public async Task<SpentInTimePeriod> GetSpentInTimePeriod(DateTime fromDate, DateTime toDate,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync(
            $"{ApiRoutes.Dashboard}/spent?from={QueryTime(fromDate)}&to={QueryTime(toDate)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<SpentInTimePeriod>(cancellationToken);
    }

    public IAsyncEnumerable<UpcomingPayment> GetUpcomingPaymentsAsync(int numberToFetch,
        CancellationToken cancellationToken) =>
        _http.GetStreamAsync<UpcomingPayment>($"{ApiRoutes.Dashboard}/upcoming-payments?count={numberToFetch}",
            cancellationToken);

    // As UTC, so the instant survives the server being in another time zone
    private static string QueryTime(DateTime time) =>
        Uri.EscapeDataString(time.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
}
