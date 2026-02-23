using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class HouseholdMember : BaseEntity
{
    [Encrypt]
    public required string FirstName { get; init; }

    [Encrypt]
    public required string LastName { get; init; }

    [Encrypt]
    public decimal? Income { get; init; }
}
