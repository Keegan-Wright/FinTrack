namespace FinanceTracker.Infrastructure.Budgets;

/// <summary>
/// How a budget is shared, as the user sees it, and the names of what it covers.
/// </summary>
/// <param name="SharedBy">Its owner's first and last name when it's someone else's, null when it's the user's.</param>
/// <param name="CanChange">Whether the user can edit it.</param>
/// <param name="IsHousehold">Whether it's shared, so counts tagged spending on every account its owner sees.</param>
/// <param name="TagNames">For someone else's, the names of its tags, by id.</param>
/// <param name="TagChoices">For someone else's they can change, the tags it can cover, by id.</param>
/// <param name="AccountNames">The names of its accounts its owner still sees, by id.</param>
/// <param name="AccountChoices">For someone else's they can change, the accounts it can cover, by id.</param>
internal sealed record BudgetSharing(
    string? SharedBy,
    bool CanChange,
    bool IsHousehold,
    Dictionary<Guid, string> TagNames,
    Dictionary<Guid, string> TagChoices,
    Dictionary<Guid, string> AccountNames,
    Dictionary<Guid, string> AccountChoices);
