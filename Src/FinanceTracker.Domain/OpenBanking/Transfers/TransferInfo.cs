using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.OpenBanking.Transfers;

/// <summary>
/// Whether a transaction is money moved between the user's own accounts, who decided and why.
/// </summary>
/// <param name="Decision">What was decided.</param>
/// <param name="Source">Who decided, a decision only replaces one from an equal or lower source.</param>
/// <param name="Reason">Why the matcher decided, null for the user's decisions.</param>
/// <param name="PartnerTransactionId">The other half of the transfer, or for review the suggested one.</param>
/// <param name="PartnerAccountId">The account the money moved to or from.</param>
/// <param name="PartnerHouseholdMemberId">The household member the money moved to or from, when their account isn't
/// connected so there's no transaction or account to name.</param>
/// <param name="DecidedAt">When it was decided.</param>
public sealed record TransferInfo(
    TransferDecision Decision,
    DecisionSource Source,
    TransferReason? Reason,
    Guid? PartnerTransactionId,
    Guid? PartnerAccountId,
    Guid? PartnerHouseholdMemberId,
    DateTime DecidedAt)
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
    /// Whether this says the same as <paramref name="other"/>, whenever each was decided.
    /// </summary>
    public bool IsSameAs(TransferInfo? other) => other is not null && this with { DecidedAt = other.DecidedAt } == other;
}
