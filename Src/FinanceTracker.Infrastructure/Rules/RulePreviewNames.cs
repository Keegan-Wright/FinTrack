namespace FinanceTracker.Infrastructure.Rules;

/// <summary>
/// The user's tags, bills and debts by id, so a preview names what would change.
/// </summary>
internal sealed record RulePreviewNames(
    IReadOnlyDictionary<Guid, string> Tags,
    IReadOnlyDictionary<Guid, string> Bills,
    IReadOnlyDictionary<Guid, string> Debts);
