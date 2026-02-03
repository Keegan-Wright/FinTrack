using System.ComponentModel;

namespace FinanceTracker.Models.Request.HouseholdMember;

public class AddHouseholdMemberRequest
{
    [Description("First name of the household member")]
    public required string FirstName { get; set; }

    [Description("Last name of the household member")]
    public required string LastName { get; set; }

    [Description("Monthly income of the household member")]
    public decimal Income { get; set; }
}