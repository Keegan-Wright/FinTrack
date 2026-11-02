using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.Debts;

/// <summary>
/// Whether a transaction pays off a <see cref="Debt"/>, who decided and when. Kept on the
/// <see cref="BankTransaction"/>, apart from its bill payment, so a car finance direct debit can pay both its bill and
/// its debt.
/// </summary>
/// <param name="DebtId">The debt it pays, null when the user says it pays none so no rule links it again.</param>
/// <param name="Source">Who decided, a rule or the user. A decision only replaces one from an equal or lower source.
/// </param>
/// <param name="DecidedAt">When it was decided.</param>
/// <param name="RuleId">The rule that decided, null unless <paramref name="Source"/> is
/// <see cref="DecisionSource.Rule"/>.</param>
public sealed record DebtPayment(Guid? DebtId, DecisionSource Source, DateTime DecidedAt, Guid? RuleId = null)
{
    public static DebtPayment FromRule(Guid debtId, Guid ruleId, DateTime decidedAt) =>
        new(debtId, DecisionSource.Rule, decidedAt, ruleId);

    /// <summary>
    /// Whether this says the same as <paramref name="other"/>, whenever each was decided.
    /// </summary>
    public bool IsSameAs(DebtPayment? other) => other is not null && this with { DecidedAt = other.DecidedAt } == other;
}
