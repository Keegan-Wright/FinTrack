using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain;

public class HouseholdMember : BaseEntity
{
    public HouseholdMember(string firstName, string lastName, decimal? income)
    {
        FirstName = firstName;
        LastName = lastName;
        Income = income;
    }

    [Encrypt] public string FirstName { get; private set; }

    [Encrypt] public string LastName { get; private set; }

    [Encrypt] public decimal? Income { get; private set; }
}
