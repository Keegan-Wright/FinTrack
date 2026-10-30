namespace FinanceTracker.Domain.Rules;

/// <summary>
/// A tag a rule added to a transaction so the transaction can show which rule tagged it.
/// </summary>
/// <param name="ClassificationId">The tag.</param>
/// <param name="RuleId">The first rule in the order that added it.</param>
public sealed record RuleTag(Guid ClassificationId, Guid RuleId);
