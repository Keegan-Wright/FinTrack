namespace FinanceTracker.Infrastructure.Rules;

/// <summary>
/// The user's tags, bills and debts by id, so a preview names what would change.
/// </summary>
/// <param name="Tags">The tags, by id.</param>
/// <param name="Bills">The bills, by id.</param>
/// <param name="Debts">The debts, by id.</param>
internal sealed record RulePreviewNames(
    IReadOnlyDictionary<Guid, string> Tags,
    IReadOnlyDictionary<Guid, string> Bills,
    IReadOnlyDictionary<Guid, string> Debts);
