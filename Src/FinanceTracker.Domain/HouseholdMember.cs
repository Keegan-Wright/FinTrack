using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain;

public class HouseholdMember : BaseEntity
{
    [Encrypt]
    public required string FirstName { get; init; }

    [Encrypt]
    public required string LastName { get; init; }

    [Encrypt]
    public decimal? Income { get; init; }
}
