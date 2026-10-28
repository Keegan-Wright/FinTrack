namespace FinanceTracker.Domain.OpenBanking.Transfers;

/// <summary>
/// What a <see cref="TransferMatcher"/> run changed.
/// </summary>
/// <param name="Transfers">Transactions newly decided to be transfers.</param>
/// <param name="ToReview">Transactions newly put up for review.</param>
/// <param name="Cleared">Earlier matcher decisions removed, so those transactions count again.</param>
public sealed record TransferMatchSummary(int Transfers, int ToReview, int Cleared);
