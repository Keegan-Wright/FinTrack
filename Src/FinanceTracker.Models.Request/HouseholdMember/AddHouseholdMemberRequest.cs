using System.ComponentModel;

namespace FinanceTracker.Models.Request.HouseholdMember;

public class AddHouseholdMemberRequest
{
    [Description("First name of the household member")]
    public required string FirstName { get; init; }

    [Description("Last name of the household member")]
    public required string LastName { get; init; }

    [Description("Monthly income of the household member")]
    public required decimal Income { get; init; }
}
