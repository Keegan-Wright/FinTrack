using FinanceTracker.Domain.OpenBanking.Transfers;

namespace FinanceTracker.Infrastructure.OpenBanking.Transfers;

/// <summary>
/// Finds the current user's transfers between their own accounts, see <see cref="TransferMatcher"/>.
/// </summary>
public interface ITransferMatchingService
{
    /// <summary>
    /// Matches across all of the current user's transactions and saves the decisions. For changes that can affect
    /// any transaction, e.g. how a household member is recognised.
    /// </summary>
    Task<TransferMatchSummary> MatchAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Matches around the transactions created or updated since the last run, e.g. after a sync. Everything on the
    /// first run.
    /// </summary>
    /// <returns>Null when nothing has changed, so there was nothing to match.</returns>
    Task<TransferMatchSummary?> MatchChangesAsync(CancellationToken cancellationToken);
}
