using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Household;

/// <summary>
/// User defined house hold members
/// A house hold member might pull more than their own finances into the app, this allowes other incomes to be factored in.
/// </summary>
public sealed class HouseholdMember : AggregateRoot
{
    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private HouseholdMember()
    {
    }
#pragma warning restore CS8618

    public HouseholdMember(Guid userId, string firstName, string lastName, decimal? income) : base(userId)
    {
        Rename(firstName, lastName);
        ChangeIncome(income);
    }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public decimal? Income { get; private set; }

    [MemberNotNull(nameof(FirstName), nameof(LastName))]
    public void Rename(string firstName, string lastName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        FirstName = firstName;
        LastName = lastName;
    }

    public void ChangeIncome(decimal? income)
    {
        if (income.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(income.Value, nameof(income));
        }

        Income = income;
    }
}
