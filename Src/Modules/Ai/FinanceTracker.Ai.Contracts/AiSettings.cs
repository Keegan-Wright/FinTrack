namespace FinanceTracker.Ai.Contracts;

/// <summary>
/// The install's AI settings, as an admin sees them.
/// </summary>
public sealed class AiSettings
{
    /// <summary>
    /// The Ollama endpoint an admin saved, null when they haven't and the environment's is used.
    /// </summary>
    public string? OllamaEndpoint { get; init; }

    /// <summary>
    /// The Ollama endpoint from the <c>OLLAMA_ENDPOINT</c> environment variable, null when it isn't set.
    /// </summary>
    public string? EnvironmentOllamaEndpoint { get; init; }
}
