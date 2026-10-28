using System.ComponentModel;

namespace FinanceTracker.Contracts.HouseholdMember;

/// <summary>
/// How to recognise money to or from a household member, replacing what was there. Validated by
/// <see cref="Validation.UpdateHouseholdMemberRecognitionValidator"/>.
/// </summary>
public class UpdateHouseholdMemberRecognition
{
    /// <summary>
    /// How the member appears in transaction descriptions, e.g. <c>J Smith</c>.
    /// </summary>
    [Description("Names the household member appears as in transaction descriptions")]
    public List<string> PayeeNames { get; set; } = [];

    /// <summary>
    /// The member's own accounts, named in the references of payments to or from them.
    /// </summary>
    [Description("The household member's own accounts")]
    public List<LinkedAccount> LinkedAccounts { get; set; } = [];
}
