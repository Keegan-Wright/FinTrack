using System.Net;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="ISharingService"/> over <see cref="ApiRoutes.SharingLinks"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<ISharingService>]
public sealed class SharingApiClient : ISharingService
{
    private readonly HttpClient _http;

    public SharingApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public IAsyncEnumerable<UserLink> GetLinksAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<UserLink>(ApiRoutes.SharingLinks, cancellationToken);

    public Task<InviteOutcome> InviteAsync(InviteToShare request, CancellationToken cancellationToken) =>
        _http.PostReadAsync<InviteToShare, InviteOutcome>(ApiRoutes.SharingLinks, request, cancellationToken);

    public Task<bool> AcceptAsync(Guid id, CancellationToken cancellationToken) =>
        PostIfFoundAsync($"{ApiRoutes.SharingLinks}/{id}/accept", cancellationToken);

    public Task<bool> DeclineAsync(Guid id, CancellationToken cancellationToken) =>
        PostIfFoundAsync($"{ApiRoutes.SharingLinks}/{id}/decline", cancellationToken);

    public Task<bool> EndAsync(Guid id, CancellationToken cancellationToken) =>
        _http.DeleteIfFoundAsync($"{ApiRoutes.SharingLinks}/{id}", cancellationToken);

    private async Task<bool> PostIfFoundAsync(string path, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PostAsync(path, null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }
}
