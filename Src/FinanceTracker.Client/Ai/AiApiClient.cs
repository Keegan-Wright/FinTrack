#if FINTRACK_AI
using System.Net.Http.Json;
using FinanceTracker.Ai.Contracts;
using FinanceTracker.Client.Api;

namespace FinanceTracker.Client.Ai;

/// <summary>
/// <see cref="IAiService"/> in the browser, calling the server. Here rather than in the AI module, which loads after
/// the app has started and so can't register services. Only built with FinTrackAi=true.
/// </summary>
public sealed class AiApiClient : IAiService
{
    private readonly HttpClient _http;

    /// <param name="httpClientFactory">Makes the client that calls the server.</param>
    public AiApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    /// <inheritdoc/>
    public async Task<AiStatus> GetStatusAsync(CancellationToken cancellationToken) =>
        (await _http.GetFromJsonAsync<AiStatus>(AiRoutes.Status, cancellationToken))!;

    /// <inheritdoc/>
    public async Task<AiStatus> ChangeOptInAsync(ChangeAiOptIn change, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PutAsJsonAsync(AiRoutes.OptIn, change, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AiStatus>(cancellationToken))!;
    }
}
#endif
