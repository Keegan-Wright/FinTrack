namespace FinanceTracker.Data.Models;


public class HouseholdMember : BaseEntity
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public decimal? Income { get; set; }
}