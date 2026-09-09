using FinanceTracker.Contracts.HouseholdMember;

namespace FinanceTracker.Infrastructure.HouseholdMembers;

public interface IHouseholdMemberService
{
    IAsyncEnumerable<HouseholdMemberResponse> GetHouseholdMembersAsync(CancellationToken cancellationToken);

    Task<HouseholdMemberResponse> AddHouseholdMemberAsync(AddHouseholdMemberRequest categoryToAdd,
        CancellationToken cancellationToken);

    Task<bool> DeleteHouseholdMemberAsync(Guid id, CancellationToken cancellationToken);
}
