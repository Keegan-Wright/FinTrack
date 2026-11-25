namespace FinanceTracker.Ai.Contracts;

/// <summary>
/// New AI settings for the install, from an admin.
/// </summary>
public sealed class SaveAiSettings
{
    /// <summary>
    /// The Ollama endpoint to use, an absolute http or https address. Empty to go back to the environment's.
    /// </summary>
    public string? OllamaEndpoint { get; set; }
}
