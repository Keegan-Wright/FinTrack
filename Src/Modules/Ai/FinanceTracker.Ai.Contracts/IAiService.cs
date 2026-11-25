namespace FinanceTracker.Ai.Contracts;

/// <summary>
/// Whether AI is set up, and the signed in user's opt in. The server's works on the database, the browser's calls
/// the server.
/// </summary>
public interface IAiService
{
    /// <summary>
    /// Whether AI is set up for the install, and whether the user has turned it on.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    Task<AiStatus> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Turns AI on or off for the user.
    /// </summary>
    /// <param name="change">Whether they want it on.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    Task<AiStatus> ChangeOptInAsync(ChangeAiOptIn change, CancellationToken cancellationToken);
}
