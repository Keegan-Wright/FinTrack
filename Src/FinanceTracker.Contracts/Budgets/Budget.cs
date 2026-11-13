using System.ComponentModel;

namespace FinanceTracker.Contracts.Budgets;

/// <summary>
/// One of the user's budgets, a monthly limit on what their tags cover.
/// </summary>
public class Budget
{
    /// <summary>
    /// The budget's id.
    /// </summary>
    [Description("Unique identifier for the budget")]
    public Guid Id { get; init; }

    /// <summary>
    /// The budget's name.
    /// </summary>
    [Description("The budget's name")]
    public required string Name { get; init; }

    /// <summary>
    /// The user's tags it covers, a transaction with any of them counts towards it.
    /// </summary>
    [Description("The tags the budget covers")]
    public List<Guid> TagIds { get; init; } = [];

    /// <summary>
    /// What it carries from one month into the next.
    /// </summary>
    [Description("What the budget carries from one month into the next")]
    public RolloverMode RolloverMode { get; init; }

    /// <summary>
    /// The limit for the current month.
    /// </summary>
    [Description("The limit for the current month")]
    public decimal MonthlyLimit { get; init; }

    /// <summary>
    /// Every limit it's had, oldest first, so past months show the limit they had.
    /// </summary>
    [Description("Every limit the budget has had, oldest first")]
    public List<BudgetLimit> Limits { get; init; } = [];

    /// <summary>
    /// The first and last name of whoever shares it with the user, null when it's their own.
    /// </summary>
    [Description("Who shares the budget with the user, empty when it's their own")]
    public string? SharedBy { get; init; }

    /// <summary>
    /// Whether the user can edit it. Their own, or shared with them with changes allowed. Only its owner deletes it.
    /// </summary>
    [Description("Whether the user can edit the budget")]
    public bool CanChange { get; init; } = true;

    /// <summary>
    /// Whether it's shared, so counts tagged spending on every account its owner sees, theirs and those shared with
    /// them, rather than only on their own.
    /// </summary>
    [Description("Whether the budget is a household budget, counting spending on every account its owner sees")]
    public bool IsHousehold { get; init; }

    /// <summary>
    /// For one someone else shares, the names of its tags, by id, as they're its owner's.
    /// </summary>
    [Description("Names of a shared budget's tags, by id")]
    public Dictionary<Guid, string> TagNames { get; init; } = [];

    /// <summary>
    /// For one someone else shares with changes allowed, the tags it can cover: its owner's tags shared with the user,
    /// and those it already covers, by id.
    /// </summary>
    [Description("The tags a shared budget the user can edit can cover, by id")]
    public Dictionary<Guid, string> TagChoices { get; init; } = [];
}
