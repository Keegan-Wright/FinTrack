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
    /// <summary>
    /// The user's notification settings.
    /// </summary>
    private const string Settings = $"{ApiRoutes.Notifications}/settings";
    /// <summary>
    /// The user's webhook.
    /// </summary>
    private const string Webhook = $"{Settings}/webhook";

    private readonly HttpClient _http;

    /// <param name="httpClientFactory">Makes the API's client.</param>
    public NotificationsApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    /// <inheritdoc/>
    public IAsyncEnumerable<Notification> GetNotificationsAsync(int take, CancellationToken cancellationToken) =>
        _http.GetStreamAsync<Notification>($"{ApiRoutes.Notifications}?take={take}", cancellationToken);

    /// <inheritdoc/>
    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.GetAsync($"{ApiRoutes.Notifications}/unread", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<int>(cancellationToken);
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async Task MarkAllReadAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PostAsync($"{ApiRoutes.Notifications}/read", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc/>
    public async Task<NotificationSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync(Settings, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<NotificationSettings>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<NotificationSettings> ChangeSettingsAsync(ChangeNotificationSettings settings,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PutAsJsonAsync(Settings, settings, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<NotificationSettings>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<NotificationSettings> ChangeAlertsAsync(ChangeAlertSettings alerts,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PutAsJsonAsync($"{Settings}/alerts", alerts, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<NotificationSettings>(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<NotificationSettings?> SendTestEmailAsync(CancellationToken cancellationToken) =>
        PostUnlessConflictAsync($"{Settings}/test-email", cancellationToken);

    /// <inheritdoc/>
    public async Task<WebhookChange> SetWebhookAsync(SetWebhook webhook, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PutAsJsonAsync(Webhook, webhook, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<WebhookChange>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<NotificationSettings> RemoveWebhookAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.DeleteAsync(Webhook, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<NotificationSettings>(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<NotificationSettings?> ReplaceWebhookSecretAsync(CancellationToken cancellationToken) =>
        PostUnlessConflictAsync($"{Webhook}/secret", cancellationToken);

    /// <inheritdoc/>
    public Task<NotificationSettings?> SendTestWebhookAsync(CancellationToken cancellationToken) =>
        PostUnlessConflictAsync($"{Webhook}/test", cancellationToken);

    /// <param name="route">The API path.</param>
    /// <param name="cancellationToken">Stops the call.</param>
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
