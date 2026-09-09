using FinanceTracker.Contracts.HouseholdMember;

namespace FinanceTracker.Infrastructure.HouseholdMembers;

public interface IHouseholdMemberService
{
    IAsyncEnumerable<HouseholdMember> GetHouseholdMembersAsync(CancellationToken cancellationToken);

    Task<HouseholdMember> AddHouseholdMemberAsync(AddHouseholdMember categoryToAdd,
        CancellationToken cancellationToken);

    Task<bool> DeleteHouseholdMemberAsync(Guid id, CancellationToken cancellationToken);
}
