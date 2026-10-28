using System.Net.Http.Json;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using HouseholdMember = FinanceTracker.Contracts.HouseholdMember.HouseholdMember;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IHouseholdMemberService"/> over <see cref="ApiRoutes.HouseholdMembers"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<IHouseholdMemberService>]
public sealed class HouseholdMemberApiClient : IHouseholdMemberService
{
    private readonly HttpClient _http;

    public HouseholdMemberApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public IAsyncEnumerable<HouseholdMember> GetHouseholdMembersAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<HouseholdMember>(ApiRoutes.HouseholdMembers, cancellationToken);

    public Task<HouseholdMember> AddHouseholdMemberAsync(AddHouseholdMember categoryToAdd,
        CancellationToken cancellationToken) =>
        _http.PostReadAsync<AddHouseholdMember, HouseholdMember>(ApiRoutes.HouseholdMembers, categoryToAdd,
            cancellationToken);

    public async Task<HouseholdMemberRecognitionSaved> UpdateRecognitionAsync(Guid id,
        UpdateHouseholdMemberRecognition request, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PutAsJsonAsync(
            $"{ApiRoutes.HouseholdMembers}/{id}/recognition", request, cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<HouseholdMemberRecognitionSaved>(cancellationToken);
    }

    public Task<bool> DeleteHouseholdMemberAsync(Guid id, CancellationToken cancellationToken) =>
        _http.DeleteIfFoundAsync($"{ApiRoutes.HouseholdMembers}/{id}", cancellationToken);
}
