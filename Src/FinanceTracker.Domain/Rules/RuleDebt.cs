namespace FinanceTracker.Domain.Rules;

/// <summary>
/// The debt a rule says a transaction pays off, see <see cref="RuleActionType.PayDebt"/>.
/// </summary>
/// <param name="DebtId">The debt it pays off.</param>
/// <param name="RuleId">The rule that said so.</param>
public sealed record RuleDebt(Guid DebtId, Guid RuleId);
