using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Automation;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IAutomationService"/> over <see cref="ApiRoutes.AutomationJobs"/> and
/// <see cref="ApiRoutes.Sync"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<IAutomationService>]
public sealed class AutomationApiClient : IAutomationService
{
    private readonly HttpClient _http;

    public AutomationApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public IAsyncEnumerable<AutomationCron> GetJobsAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<AutomationCron>(ApiRoutes.AutomationJobs, cancellationToken);

    public async Task<bool> UpdateJobSettingsAsync(UpdateCronJob request, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PutAsJsonAsync($"{ApiRoutes.AutomationJobs}/{request.Id}",
            request, cancellationToken);
        response.EnsureFound();
        return true;
    }

    public async Task<DateTime?> GetLastSyncTimeAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync($"{ApiRoutes.Sync}/last", cancellationToken);
        response.EnsureSuccessStatusCode();

        // No content when the user's accounts have never synced
        return response.StatusCode == HttpStatusCode.NoContent
            ? null
            : await response.ReadRequiredJsonAsync<DateTime>(cancellationToken);
    }

    public async Task RequestSyncAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PostAsync(ApiRoutes.Sync, content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
