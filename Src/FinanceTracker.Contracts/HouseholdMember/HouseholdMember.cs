using System.ComponentModel;

namespace FinanceTracker.Contracts.HouseholdMember;

/// <summary>
/// A member of the user's household.
/// </summary>
public class HouseholdMember
{
    /// <summary>
    /// The member's id.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The member's first name.
    /// </summary>
    public required string FirstName { get; init; }

    /// <summary>
    /// The member's last name.
    /// </summary>
    public required string LastName { get; init; }

    /// <summary>
    /// What the member earns a year, null when not given.
    /// </summary>
    public decimal? YearlyIncome { get; init; }

    /// <summary>
    /// How the member appears in transaction descriptions.
    /// </summary>
    public IReadOnlyList<string> PayeeNames { get; init; } = [];

    /// <summary>
    /// The member's own accounts, which aren't connected to the app.
    /// </summary>
    public IReadOnlyList<LinkedAccount> LinkedAccounts { get; init; } = [];

    /// <summary>
    /// The FinTrack user the member is, someone the user is linked with, null when they aren't one.
    /// </summary>
    [Description("The user the member is linked to, empty when they aren't one")]
    public Guid? LinkedUserId { get; init; }

    /// <summary>
    /// The linked user's username, null when they aren't one.
    /// </summary>
    [Description("The linked user's username, empty when they aren't one")]
    public string? LinkedUsername { get; init; }
}
