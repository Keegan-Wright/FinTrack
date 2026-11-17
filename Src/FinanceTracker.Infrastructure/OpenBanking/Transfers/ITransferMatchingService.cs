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
    /// <param name="cancellationToken">Stops the matching.</param>
    Task<TransferMatchSummary> MatchAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Matches around the transactions created or updated since the last run, e.g. after a sync. Everything on the
    /// first run.
    /// </summary>
    /// <param name="cancellationToken">Stops the matching.</param>
    /// <returns>Null when nothing has changed, so there was nothing to match.</returns>
    Task<TransferMatchSummary?> MatchChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Matches around <paramref name="transactionIds"/>, e.g. after the user decides about them, so the pairs they
    /// affect update straight away. Leaves when the last run was alone, so it doesn't stand in for matching a sync.
    /// </summary>
    /// <param name="transactionIds">The transactions that changed.</param>
    /// <param name="cancellationToken">Stops the matching.</param>
    Task<TransferMatchSummary> MatchAroundAsync(IReadOnlyCollection<Guid> transactionIds,
        CancellationToken cancellationToken);
}
