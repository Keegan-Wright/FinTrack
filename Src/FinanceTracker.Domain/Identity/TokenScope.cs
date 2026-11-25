namespace FinanceTracker.Domain.Identity;

/// <summary>
/// What a personal access token may do.
/// </summary>
public enum TokenScope
{
    /// <summary>
    /// Read the user's data through the API, never change it.
    /// </summary>
    Read,
#if FINTRACK_AI

    /// <summary>
    /// Used by AI clients through MCP, and nothing else. Only in the AI build.
    /// </summary>
    Ai
#endif
}
