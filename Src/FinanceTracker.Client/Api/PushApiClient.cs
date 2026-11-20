using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IPushService"/> over <see cref="ApiRoutes.Push"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<IPushService>]
public sealed class PushApiClient : IPushService
{
    /// <summary>
    /// The user's devices.
    /// </summary>
    private const string Devices = $"{ApiRoutes.Push}/devices";

    private readonly HttpClient _http;

    /// <param name="httpClientFactory">Makes the API's client.</param>
    public PushApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    /// <inheritdoc/>
    public async Task<PushSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync(ApiRoutes.Push, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<PushSettings>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PushDeviceAdded> AddDeviceAsync(AddPushDevice device, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(Devices, device, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<PushDeviceAdded>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PushSettings?> RemoveDeviceAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.DeleteAsync($"{Devices}/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<PushSettings>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PushSettings?> SendTestAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PostAsync($"{ApiRoutes.Push}/test", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<PushSettings>(cancellationToken);
    }
}
