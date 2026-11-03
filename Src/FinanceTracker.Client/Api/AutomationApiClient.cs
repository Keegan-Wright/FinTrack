using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Automation;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IAutomationService"/> over <see cref="ApiRoutes.AutomationJobs"/>, <see cref="ApiRoutes.AutomationRuns"/> and
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

    public IAsyncEnumerable<AutomationJobSummary> GetJobSummariesAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<AutomationJobSummary>($"{ApiRoutes.AutomationJobs}/summaries", cancellationToken);

    public async Task<AutomationRunPage> GetRunsAsync(string function, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        string query = $"function={Uri.EscapeDataString(function)}&page={page}&pageSize={pageSize}";
        using HttpResponseMessage response = await _http.GetAsync($"{ApiRoutes.AutomationRuns}?{query}",
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<AutomationRunPage>(cancellationToken);
    }

    public async Task RunNowAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PostAsync($"{ApiRoutes.AutomationJobs}/{id}/run",
            content: null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new JobAlreadyRunningException(await FunctionOfAsync(id, cancellationToken));
        }

        response.EnsureFound();
    }

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

    // The 409 only says the job is running, its function comes from the job list
    private async Task<string> FunctionOfAsync(Guid id, CancellationToken cancellationToken)
    {
        await foreach (AutomationCron job in GetJobsAsync(cancellationToken))
        {
            if (job.Id == id)
            {
                return job.Function;
            }
        }

        return id.ToString();
    }
}
