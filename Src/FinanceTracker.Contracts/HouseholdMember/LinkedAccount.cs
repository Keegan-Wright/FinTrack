using System.ComponentModel;

namespace FinanceTracker.Contracts.HouseholdMember;

/// <summary>
/// One of a household member's own accounts, which isn't connected to the app.
/// </summary>
public class LinkedAccount
{
    /// <summary>
    /// The 6 digit sort code, separators such as <c>11-22-33</c> allowed.
    /// </summary>
    [Description("Sort code of the household member's account")]
    public required string SortCode { get; set; }

    /// <summary>
    /// The 8 digit account number.
    /// </summary>
    [Description("Account number of the household member's account")]
    public required string Number { get; set; }
}
