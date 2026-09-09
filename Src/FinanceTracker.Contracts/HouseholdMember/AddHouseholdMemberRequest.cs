using System.ComponentModel;

namespace FinanceTracker.Contracts.HouseholdMember;

public class AddHouseholdMemberRequest
{
    [Description("First name of the household member")]
    public required string FirstName { get; set; }

    [Description("Last name of the household member")]
    public required string LastName { get; set; }

    [Description("Monthly income of the household member")]
    public required decimal Income { get; set; }
}
