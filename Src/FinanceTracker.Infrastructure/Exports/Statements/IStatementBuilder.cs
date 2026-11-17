namespace FinanceTracker.Infrastructure.Exports.Statements;

/// <summary>
/// Gathers a <see cref="Statement"/>, only the requesting users data.
/// </summary>
public interface IStatementBuilder
{
    /// <summary>
    /// The statement for the pay period named after <paramref name="month"/> of <paramref name="year"/>.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month of the year, 1 to 12.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    Task<Statement> BuildAsync(int year, int month, CancellationToken cancellationToken);
}
