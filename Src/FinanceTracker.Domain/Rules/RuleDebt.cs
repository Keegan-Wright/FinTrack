namespace FinanceTracker.Domain.Rules;

/// <summary>
/// The debt a rule says a transaction pays off, see <see cref="RuleActionType.PayDebt"/>.
/// </summary>
public sealed record RuleDebt(Guid DebtId, Guid RuleId);
