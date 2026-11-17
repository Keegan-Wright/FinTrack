using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.Bills;

/// <summary>
/// Whether a transaction pays a <see cref="Bill"/>, who decided and when. Kept on the
/// <see cref="BankTransaction"/>, like its transfer answer.
/// </summary>
/// <param name="BillId">The bill it pays, null when the user says it pays none so nothing links it again.</param>
/// <param name="Source">Who decided, a decision only replaces one from an equal or lower source.</param>
/// <param name="DecidedAt">When it was decided.</param>
/// <param name="RuleId">The rule that decided, null unless <paramref name="Source"/> is
/// <see cref="DecisionSource.Rule"/>.</param>
public sealed record BillPayment(Guid? BillId, DecisionSource Source, DateTime DecidedAt, Guid? RuleId = null)
{
    /// <summary>
    /// The matcher decided it pays <paramref name="billId"/>.
    /// </summary>
    /// <param name="billId">The bill it pays.</param>
    /// <param name="decidedAt">When it was decided.</param>
    public static BillPayment Matched(Guid billId, DateTime decidedAt) =>
        new(billId, DecisionSource.Matcher, decidedAt);

    /// <summary>
    /// A rule decided it pays <paramref name="billId"/>.
    /// </summary>
    /// <param name="billId">The bill it pays.</param>
    /// <param name="ruleId">The rule that decided.</param>
    /// <param name="decidedAt">When it was decided.</param>
    public static BillPayment FromRule(Guid billId, Guid ruleId, DateTime decidedAt) =>
        new(billId, DecisionSource.Rule, decidedAt, ruleId);

    /// <summary>
    /// Whether this says the same as <paramref name="other"/>, whenever each was decided.
    /// </summary>
    /// <param name="other">The decision to compare with.</param>
    public bool IsSameAs(BillPayment? other) => other is not null && this with { DecidedAt = other.DecidedAt } == other;
}
