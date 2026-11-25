namespace FinanceTracker.Ai.Contracts;

/// <summary>
/// The AI module's API addresses.
/// </summary>
public static class AiRoutes
{
    /// <summary>
    /// Whether AI is set up for the install, and whether the signed in user has turned it on.
    /// </summary>
    public const string Status = "api/ai/status";

    /// <summary>
    /// Turns AI on or off for the signed in user.
    /// </summary>
    public const string OptIn = "api/ai/opt-in";

    /// <summary>
    /// The install's AI settings, admins only.
    /// </summary>
    public const string Settings = "api/ai/settings";
}
