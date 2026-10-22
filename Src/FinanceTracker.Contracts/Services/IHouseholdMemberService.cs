
namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The people in the signed in user's household and their incomes.
/// </summary>
public interface IHouseholdMemberService
{
    /// <summary>
    /// All of the user's household members, in no particular order.
    /// </summary>
    IAsyncEnumerable<HouseholdMember.HouseholdMember> GetHouseholdMembersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Adds a household member for the user. Validate the request with
    /// <see cref="Validation.AddHouseholdMemberRequestValidator"/> first.
    /// </summary>
    /// <returns>The member as stored.</returns>
    Task<HouseholdMember.HouseholdMember> AddHouseholdMemberAsync(HouseholdMember.AddHouseholdMember categoryToAdd,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one of the user's household members.
    /// </summary>
    /// <returns><see langword="false"/> if the user has no member with <paramref name="id"/>.</returns>
    Task<bool> DeleteHouseholdMemberAsync(Guid id, CancellationToken cancellationToken);
}
