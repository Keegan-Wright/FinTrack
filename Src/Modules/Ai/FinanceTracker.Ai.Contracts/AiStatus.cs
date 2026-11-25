namespace FinanceTracker.Ai.Contracts;

/// <summary>
/// Where AI stands for the signed in user.
/// </summary>
public sealed class AiStatus
{
    /// <summary>
    /// Whether the install has AI set up, an Ollama endpoint to talk to.
    /// </summary>
    public bool Available { get; init; }

    /// <summary>
    /// Whether the user has turned AI on for themselves. Off until they do.
    /// </summary>
    public bool OptedIn { get; init; }
}
