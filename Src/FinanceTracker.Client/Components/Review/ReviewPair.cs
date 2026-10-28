using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Client.Components.Review;

/// <summary>
/// A transaction waiting for review, with its suggested other half when that's waiting too, see
/// <see cref="ReviewPairs"/>.
/// </summary>
/// <param name="First">The money out when there's a pair, so pairs read the same way round.</param>
/// <param name="Second">The suggested other half, null when there isn't one waiting.</param>
public sealed record ReviewPair(Transaction First, Transaction? Second)
{
    /// <summary>
    /// Both halves, or just the one.
    /// </summary>
    public IEnumerable<Transaction> Transactions => Second is null ? [First] : [First, Second];
}
