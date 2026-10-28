
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
    /// Replaces how to recognise money to or from one of the user's household members, then re-matches the user's
    /// transfers so it applies straight away. Validate the request with
    /// <see cref="Validation.UpdateHouseholdMemberRecognitionValidator"/> first.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no member with <paramref name="id"/>.</exception>
    Task<HouseholdMember.HouseholdMemberRecognitionSaved> UpdateRecognitionAsync(Guid id,
        HouseholdMember.UpdateHouseholdMemberRecognition request, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one of the user's household members.
    /// </summary>
    /// <returns><see langword="false"/> if the user has no member with <paramref name="id"/>.</returns>
    Task<bool> DeleteHouseholdMemberAsync(Guid id, CancellationToken cancellationToken);
}
