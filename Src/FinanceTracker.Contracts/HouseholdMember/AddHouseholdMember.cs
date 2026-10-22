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
    /// The member's monthly income, 0 or more.
    /// </summary>
    [Description("Monthly income of the household member")]
    public required decimal Income { get; set; }
}
