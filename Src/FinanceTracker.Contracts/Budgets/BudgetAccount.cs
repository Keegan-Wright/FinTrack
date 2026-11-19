using System.ComponentModel;

namespace FinanceTracker.Contracts.Budgets;

/// <summary>
/// An account the user can give one of their budgets, their own or shared with them, cards included.
/// </summary>
public class BudgetAccount
{
    /// <summary>
    /// The account's id.
    /// </summary>
    [Description("The account's id")]
    public Guid AccountId { get; init; }

    /// <summary>
    /// What it's called, with whose it is when someone else shares it, e.g. <c>Joint · Jane Doe's</c>.
    /// </summary>
    [Description("The account's name, with whose it is when someone else shares it")]
    public required string Name { get; init; }
}
