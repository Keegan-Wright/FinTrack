using System.ComponentModel;

namespace FinanceTracker.Contracts.HouseholdMember;

/// <summary>
/// Which person a household member is, by the user's link with them, or none to unlink.
/// </summary>
public class LinkHouseholdMember
{
    /// <summary>
    /// The user's accepted link with the person the member is, null to unlink them.
    /// </summary>
    [Description("The link with the person the member is, empty to unlink")]
    public Guid? LinkId { get; init; }
}
