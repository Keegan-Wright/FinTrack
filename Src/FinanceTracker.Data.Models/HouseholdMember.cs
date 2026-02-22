using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class HouseholdMember : BaseEntity
{
    [Encrypt]
    public required string FirstName { get; set; }

    [Encrypt]
    public required string LastName { get; set; }

    [Encrypt]
    public decimal? Income { get; set; }
}
