namespace FinanceTracker.Models.Response.HouseholdMember;

public class HouseholdMemberResponse
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public decimal? Income { get; set; }
}