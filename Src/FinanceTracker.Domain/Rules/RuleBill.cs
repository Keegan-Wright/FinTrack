namespace FinanceTracker.Domain.Rules;

/// <summary>
/// The bill a rule says a transaction pays, see <see cref="RuleActionType.PayBill"/>.
/// </summary>
/// <param name="BillId">The bill it pays.</param>
/// <param name="RuleId">The rule that said so.</param>
public sealed record RuleBill(Guid BillId, Guid RuleId);
