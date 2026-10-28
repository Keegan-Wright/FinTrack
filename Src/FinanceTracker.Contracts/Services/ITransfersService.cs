using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's decisions about which transactions are transfers, and the ones waiting for them.
/// </summary>
/// <remarks>
/// Each decision re-matches the transactions around it, so a pair it affects updates straight away. The user's
/// decisions are never changed by FinTrack.
/// </remarks>
public interface ITransfersService
{
    /// <summary>
    /// Transactions that might be transfers and are waiting for the user, newest first, each with its suggested
    /// other half when there is one.
    /// </summary>
    IAsyncEnumerable<Transaction> GetTransfersToReviewAsync(CancellationToken cancellationToken);

    /// <summary>
    /// How many transactions are waiting for review.
    /// </summary>
    Task<int> GetReviewCountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The user says it's a transfer. A suggested other half that names it back is confirmed with it.
    /// </summary>
    /// <returns>The transactions whose transfer changed, as they are now.</returns>
    /// <exception cref="InvalidOperationException">The user has no transaction with <paramref name="id"/>.</exception>
    Task<IReadOnlyList<Transaction>> MarkAsTransferAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The user says it counts as income or spending.
    /// </summary>
    /// <returns>The transactions whose transfer changed, as they are now.</returns>
    /// <exception cref="InvalidOperationException">The user has no transaction with <paramref name="id"/>.</exception>
    Task<IReadOnlyList<Transaction>> MarkNotATransferAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the user's decision, along with the other half's when they were confirmed together, so FinTrack
    /// decides again.
    /// </summary>
    /// <returns>The transactions whose transfer changed, as they are now.</returns>
    /// <exception cref="InvalidOperationException">The user has no transaction with <paramref name="id"/>.</exception>
    Task<IReadOnlyList<Transaction>> ResetTransferAsync(Guid id, CancellationToken cancellationToken);
}
