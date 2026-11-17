namespace FinanceTracker.Infrastructure.Exports.Statements;

/// <summary>
/// The pay periods there's a statement for today's back to the one the user's oldest transaction is in.
/// </summary>
public interface IStatementPeriods
{
    /// <summary>
    /// Newest first, just today's when the user has no transactions yet.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    Task<List<Contracts.PayPeriods.PayPeriod>> GetAsync(CancellationToken cancellationToken);
}
