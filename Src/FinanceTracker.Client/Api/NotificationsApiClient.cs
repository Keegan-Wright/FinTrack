using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="INotificationsService"/> over <see cref="ApiRoutes.Notifications"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<INotificationsService>]
public sealed class NotificationsApiClient : INotificationsService
{
    private const string Settings = $"{ApiRoutes.Notifications}/settings";

    private readonly HttpClient _http;

    public NotificationsApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public IAsyncEnumerable<Notification> GetNotificationsAsync(int take, CancellationToken cancellationToken) =>
        _http.GetStreamAsync<Notification>($"{ApiRoutes.Notifications}?take={take}", cancellationToken);

    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.GetAsync($"{ApiRoutes.Notifications}/unread", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<int>(cancellationToken);
    }

    public async Task<bool> MarkReadAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PostAsync($"{ApiRoutes.Notifications}/{id}/read", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PostAsync($"{ApiRoutes.Notifications}/read", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<NotificationSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync(Settings, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<NotificationSettings>(cancellationToken);
    }

    public async Task<NotificationSettings> ChangeSettingsAsync(ChangeNotificationSettings settings,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PutAsJsonAsync(Settings, settings, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<NotificationSettings>(cancellationToken);
    }
}
