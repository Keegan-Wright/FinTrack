using System.Net;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Users;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IUsersService"/> over <see cref="ApiRoutes.Users"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<IUsersService>]
public sealed class UsersApiClient : IUsersService
{
    private readonly HttpClient _http;

    public UsersApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public IAsyncEnumerable<UserSummary> GetUsersAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<UserSummary>(ApiRoutes.Users, cancellationToken);

    public async Task<ResetLink?> CreateResetLinkAsync(Guid userId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PostAsync($"{ApiRoutes.Users}/{userId}/reset-link", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"There's no user {userId}");
        }

        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<ResetLink>(cancellationToken);
    }
}
