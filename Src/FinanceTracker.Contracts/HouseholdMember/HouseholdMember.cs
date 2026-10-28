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
    /// The member's monthly income.
    /// </summary>
    public decimal? Income { get; init; }

    /// <summary>
    /// How the member appears in transaction descriptions.
    /// </summary>
    public IReadOnlyList<string> PayeeNames { get; init; } = [];

    /// <summary>
    /// The member's own accounts, which aren't connected to the app.
    /// </summary>
    public IReadOnlyList<LinkedAccount> LinkedAccounts { get; init; } = [];
}
