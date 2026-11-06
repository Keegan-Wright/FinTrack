using System.ComponentModel;

namespace FinanceTracker.Contracts.HouseholdMember;

/// <summary>
/// A new member of the user's household. Validated by <see cref="Validation.AddHouseholdMemberRequestValidator"/>.
/// </summary>
public class AddHouseholdMember
{
    /// <summary>
    /// The member's first name.
    /// </summary>
    [Description("First name of the household member")]
    public required string FirstName { get; set; }

    /// <summary>
    /// The member's last name.
    /// </summary>
    [Description("Last name of the household member")]
    public required string LastName { get; set; }

    /// <summary>
    /// What the member earns a year, 0 or more. The dashboard works out its share for the range it shows.
    /// </summary>
    [Description("Yearly income of the household member")]
    public required decimal YearlyIncome { get; set; }
}
