using FinanceTracker.Models.Request.HouseholdMember;
using FinanceTracker.Models.Response.HouseholdMember;

namespace FinanceTracker.Services.HouseholdMembers;

public interface IHouseholdMemberService
{
    IAsyncEnumerable<HouseholdMemberResponse> GetHouseholdMembersAsync(CancellationToken cancellationToken);

    Task<HouseholdMemberResponse> AddHouseholdMemberAsync(AddHouseholdMemberRequest categoryToAdd, CancellationToken cancellationToken);
    Task<bool> DeleteHouseholdMemberAsync(Guid id, CancellationToken cancellationToken);
}