using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Calendar;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="ICalendarService"/> over <see cref="ApiRoutes.Calendar"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.ApiClient)]
[Scoped<ICalendarService>]
public sealed class CalendarApiClient : ICalendarService
{
    private readonly HttpClient _http;

    public CalendarApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public IAsyncEnumerable<CalendarItems> GetMonthItemsAsync(int month, int year,
        CancellationToken cancellationToken) =>
        _http.GetStreamAsync<CalendarItems>($"{ApiRoutes.Calendar}/{year}/{month}", cancellationToken);
}
