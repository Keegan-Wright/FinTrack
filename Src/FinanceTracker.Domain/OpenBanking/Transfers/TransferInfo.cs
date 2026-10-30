using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.OpenBanking.Transfers;

/// <summary>
/// Whether a transaction is money moved between the user's own accounts, who decided and why.
/// </summary>
/// <param name="Decision">What was decided.</param>
/// <param name="Source">Who decided, a decision only replaces one from an equal or lower source.</param>
/// <param name="Reason">Why the matcher decided, null for the user's and rules' decisions.</param>
/// <param name="PartnerTransactionId">The other half of the transfer, or for review the suggested one.</param>
/// <param name="PartnerAccountId">The account the money moved to or from.</param>
/// <param name="PartnerHouseholdMemberId">The household member the money moved to or from, when their account isn't
/// connected so there's no transaction or account to name.</param>
/// <param name="DecidedAt">When it was decided.</param>
/// <param name="RuleId">The rule that decided, null unless <paramref name="Source"/> is
/// <see cref="DecisionSource.Rule"/>.</param>
public sealed record TransferInfo(
    TransferDecision Decision,
    DecisionSource Source,
    TransferReason? Reason,
    Guid? PartnerTransactionId,
    Guid? PartnerAccountId,
    Guid? PartnerHouseholdMemberId,
    DateTime DecidedAt,
    Guid? RuleId = null)
{
    public static TransferInfo Matched(TransferReason reason, Guid? partnerTransactionId, Guid partnerAccountId,
        DateTime decidedAt) =>
        new(TransferDecision.Transfer, DecisionSource.Matcher, reason, partnerTransactionId, partnerAccountId, null,
            decidedAt);

    public static TransferInfo ToReview(TransferReason reason, Guid? partnerTransactionId, Guid? partnerAccountId,
        DateTime decidedAt) =>
        new(TransferDecision.NeedsReview, DecisionSource.Matcher, reason, partnerTransactionId, partnerAccountId, null,
            decidedAt);

    public static TransferInfo WithHouseholdMember(Guid householdMemberId, DateTime decidedAt) =>
        new(TransferDecision.Transfer, DecisionSource.Matcher, TransferReason.HouseholdMember, null, null,
            householdMemberId, decidedAt);

    /// <summary>
    /// A rule's decision, which has no partner rule says what a transaction is, not where the money went.
    /// </summary>
    public static TransferInfo FromRule(TransferDecision decision, Guid ruleId, DateTime decidedAt) =>
        new(decision, DecisionSource.Rule, null, null, null, null, decidedAt, ruleId);

    /// <summary>
    /// Whether this says the same as <paramref name="other"/>, whenever each was decided.
    /// </summary>
    public bool IsSameAs(TransferInfo? other) => other is not null && this with { DecidedAt = other.DecidedAt } == other;
}
