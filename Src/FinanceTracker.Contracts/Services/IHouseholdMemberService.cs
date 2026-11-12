
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
    /// <exception cref="NameTakenException">Someone in the household already has that first and last name, in any
    /// case.</exception>
    Task<HouseholdMember.HouseholdMember> AddHouseholdMemberAsync(HouseholdMember.AddHouseholdMember categoryToAdd,
        CancellationToken cancellationToken);

    /// <summary>
    /// Changes one of the user's household members' name and income.
    /// </summary>
    /// <returns>The member as stored.</returns>
    /// <exception cref="InvalidOperationException">The user has no member with <paramref name="id"/>.</exception>
    /// <exception cref="NameTakenException">Someone else in the household already has that first and last name, in
    /// any case.</exception>
    Task<HouseholdMember.HouseholdMember> UpdateHouseholdMemberAsync(Guid id,
        HouseholdMember.UpdateHouseholdMember request, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces how to recognise money to or from one of the user's household members, then re-matches the user's
    /// transfers so it applies straight away. Validate the request with
    /// <see cref="Validation.UpdateHouseholdMemberRecognitionValidator"/> first.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no member with <paramref name="id"/>.</exception>
    Task<HouseholdMember.HouseholdMemberRecognitionSaved> UpdateRecognitionAsync(Guid id,
        HouseholdMember.UpdateHouseholdMemberRecognition request, CancellationToken cancellationToken);

    /// <summary>
    /// Links one of the user's household members to someone they're linked with, the member taking their name, or
    /// unlinks them, keeping the name.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no such member, or no accepted link with
    /// <see cref="HouseholdMember.LinkHouseholdMember.LinkId"/>.</exception>
    /// <exception cref="NameTakenException">Another of their members already has that person's name.</exception>
    Task<HouseholdMember.HouseholdMember> LinkHouseholdMemberAsync(Guid id, HouseholdMember.LinkHouseholdMember request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one of the user's household members.
    /// </summary>
    /// <returns><see langword="false"/> if the user has no member with <paramref name="id"/>.</returns>
    Task<bool> DeleteHouseholdMemberAsync(Guid id, CancellationToken cancellationToken);
}
