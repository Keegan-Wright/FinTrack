using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain.Household;

/// <summary>
/// User defined house hold members
/// A house hold member might pull more than their own finances into the app, this allowes other incomes to be factored in.
/// </summary>
public sealed class HouseholdMember : AggregateRoot
{
    [Obsolete("EF Use Only")]
    private HouseholdMember()
    {
    }

    public HouseholdMember(Guid userId, string firstName, string lastName, decimal? income) : base(userId)
    {
        Rename(firstName, lastName);
        ChangeIncome(income);
    }

    [Encrypt] public string FirstName { get; private set; }

    [Encrypt] public string LastName { get; private set; }

    [Encrypt] public decimal? Income { get; private set; }

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
