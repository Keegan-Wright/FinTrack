using System.Net.Http.Json;
using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Calendar;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="ICalendarService"/> over <see cref="ApiRoutes.Calendar"/>. The API's 404s come back as the
/// <see cref="InvalidOperationException"/> the interface documents.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<ICalendarService>]
public sealed class CalendarApiClient : ICalendarService
{
    /// <summary>
    /// The calendar's events.
    /// </summary>
    private const string Events = $"{ApiRoutes.Calendar}/events";

    private readonly HttpClient _http;

    /// <param name="httpClientFactory">Makes the API's client.</param>
    public CalendarApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    /// <inheritdoc/>
    public IAsyncEnumerable<CalendarItems> GetMonthItemsAsync(int month, int year,
        CancellationToken cancellationToken) =>
        _http.GetStreamAsync<CalendarItems>($"{ApiRoutes.Calendar}/{year}/{month}", cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<CalendarEvent> GetEventsAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<CalendarEvent>(Events, cancellationToken);

    /// <inheritdoc/>
    public async Task<CalendarEvent> AddEventAsync(SaveCalendarEvent calendarEvent,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(Events, calendarEvent, cancellationToken);
        await response.EnsureNameFreeAsync(NameTakenException.SavingsGoal, calendarEvent.Title, cancellationToken);
        return await response.ReadRequiredJsonAsync<CalendarEvent>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CalendarEvent> UpdateEventAsync(Guid id, SaveCalendarEvent calendarEvent,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PutAsJsonAsync($"{Events}/{id}", calendarEvent, cancellationToken);
        await response.EnsureNameFreeAsync(NameTakenException.SavingsGoal, calendarEvent.Title, cancellationToken);
        return await response.ReadRequiredJsonAsync<CalendarEvent>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteEventAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.DeleteAsync($"{Events}/{id}", cancellationToken);
        await response.EnsureFoundAsync(cancellationToken);
    }
}
