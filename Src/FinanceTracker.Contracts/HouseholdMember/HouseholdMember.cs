namespace FinanceTracker.Contracts.HouseholdMember;

public class HouseholdMember
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public decimal? Income { get; init; }
}
