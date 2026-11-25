namespace FinanceTracker.Ai.Contracts;

/// <summary>
/// The AI module's settings and the signed in user's opt in. The server's works on the database, the browser's calls
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

    /// <summary>
    /// The install's AI settings. Admins only.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    Task<AiSettings> GetSettingsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Saves the install's AI settings. Admins only.
    /// </summary>
    /// <param name="settings">The new settings.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    Task<AiSettings> SaveSettingsAsync(SaveAiSettings settings, CancellationToken cancellationToken);
}
