namespace FinanceTracker.Domain.Rules;

/// <summary>
/// The bill a rule says a transaction pays, see <see cref="RuleActionType.PayBill"/>.
/// </summary>
public sealed record RuleBill(Guid BillId, Guid RuleId);
