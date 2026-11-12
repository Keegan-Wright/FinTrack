using System.Net;
using System.Net.Http.Json;
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

    public async Task<LinkShares?> GetSharesAsync(Guid linkId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.GetAsync($"{ApiRoutes.SharingLinks}/{linkId}/shares", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<LinkShares>(cancellationToken);
    }

    public async Task<LinkShares> ChangeAccountSharesAsync(Guid linkId, ChangeAccountShares request,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PutAsJsonAsync(
            $"{ApiRoutes.SharingLinks}/{linkId}/shares/accounts", request, cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<LinkShares>(cancellationToken);
    }

    public async Task<LinkShares> ChangeItemSharesAsync(Guid linkId, ChangeItemShares request,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PutAsJsonAsync(
            $"{ApiRoutes.SharingLinks}/{linkId}/shares/items", request, cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<LinkShares>(cancellationToken);
    }

    public async Task<LinkShares> MergeJointAccountsAsync(Guid linkId, MergeJointAccounts request,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(
            $"{ApiRoutes.SharingLinks}/{linkId}/joint-accounts", request, cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<LinkShares>(cancellationToken);
    }

    public async Task<LinkShares> SplitJointAccountAsync(Guid linkId, Guid jointAccountId,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.DeleteAsync(
            $"{ApiRoutes.SharingLinks}/{linkId}/joint-accounts/{jointAccountId}", cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<LinkShares>(cancellationToken);
    }

    public async Task<SharingView> GetViewAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync(ApiRoutes.SharingView, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<SharingView>(cancellationToken);
    }

    public async Task<SharingView> ChangeViewAsync(ChangeSharingView request, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PutAsJsonAsync(ApiRoutes.SharingView, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<SharingView>(cancellationToken);
    }

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
