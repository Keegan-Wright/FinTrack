using FinanceTracker.Domain.OpenBanking.Transfers;

namespace FinanceTracker.Domain.Rules;

/// <summary>
/// The transfer answer a rule gave a transaction.
/// </summary>
/// <param name="Decision"><see cref="TransferDecision.Transfer"/> or <see cref="TransferDecision.NotATransfer"/>.
/// </param>
/// <param name="RuleId">The rule that decided.</param>
public sealed record RuleTransfer(TransferDecision Decision, Guid RuleId);
