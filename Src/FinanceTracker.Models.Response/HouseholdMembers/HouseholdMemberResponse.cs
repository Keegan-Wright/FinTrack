namespace FinanceTracker.Models.Response.HouseholdMembers;

public class HouseholdMemberResponse
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public decimal? Income { get; init; }
}
