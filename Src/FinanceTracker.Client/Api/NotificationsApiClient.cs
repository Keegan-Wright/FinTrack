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
    private const string Webhook = $"{Settings}/webhook";

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

    public Task<NotificationSettings?> SendTestEmailAsync(CancellationToken cancellationToken) =>
        PostUnlessConflictAsync($"{Settings}/test-email", cancellationToken);

    public async Task<WebhookChange> SetWebhookAsync(SetWebhook webhook, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PutAsJsonAsync(Webhook, webhook, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<WebhookChange>(cancellationToken);
    }

    public async Task<NotificationSettings> RemoveWebhookAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.DeleteAsync(Webhook, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<NotificationSettings>(cancellationToken);
    }

    public Task<NotificationSettings?> ReplaceWebhookSecretAsync(CancellationToken cancellationToken) =>
        PostUnlessConflictAsync($"{Webhook}/secret", cancellationToken);

    public Task<NotificationSettings?> SendTestWebhookAsync(CancellationToken cancellationToken) =>
        PostUnlessConflictAsync($"{Webhook}/test", cancellationToken);

    /// <returns>The settings, null when the server answers 409, there's nothing to do it to.</returns>
    private async Task<NotificationSettings?> PostUnlessConflictAsync(string route,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PostAsync(route, null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<NotificationSettings>(cancellationToken);
    }
}
