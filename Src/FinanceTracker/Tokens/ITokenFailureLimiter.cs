namespace FinanceTracker.Tokens;

/// <summary>
/// Counts refused personal access tokens from each address, so an address that keeps guessing has its tokens
/// refused without being looked at. Only refusals count, so a script polling with a good token is never limited.
/// </summary>
public interface ITokenFailureLimiter
{
    /// <summary>
    /// How long the count lasts before it starts again, which a blocked address is told to wait.
    /// </summary>
    TimeSpan Window { get; }

    /// <summary>
    /// Whether <paramref name="address"/> has used up its refusals for now.
    /// </summary>
    /// <param name="address">The request's address.</param>
    bool IsBlocked(string address);

    /// <summary>
    /// Counts a refused token from <paramref name="address"/>.
    /// </summary>
    /// <param name="address">The request's address.</param>
    void RecordFailure(string address);
}
