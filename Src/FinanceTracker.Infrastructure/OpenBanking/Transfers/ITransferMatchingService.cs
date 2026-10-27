using FinanceTracker.Domain.OpenBanking.Transfers;

namespace FinanceTracker.Infrastructure.OpenBanking.Transfers;

/// <summary>
/// Finds the current user's transfers between their own accounts, see <see cref="TransferMatcher"/>.
/// </summary>
public interface ITransferMatchingService
{
    /// <summary>
    /// Matches across all of the current user's transactions and saves the decisions.
    /// </summary>
    Task<TransferMatchSummary> MatchAsync(CancellationToken cancellationToken);
}
