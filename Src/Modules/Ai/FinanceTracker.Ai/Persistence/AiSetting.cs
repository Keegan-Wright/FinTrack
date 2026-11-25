namespace FinanceTracker.Ai.Persistence;

/// <summary>
/// One of the AI module's app-wide settings. Each has a row from the start, see <see cref="All"/>, so saving one is
/// always an update.
/// </summary>
public class AiSetting
{
    /// <summary>
    /// The Ollama server everyone's AI runs on. Empty uses the <c>OLLAMA_ENDPOINT</c> environment variable.
    /// </summary>
    public const string OllamaEndpoint = "OllamaEndpoint";

    /// <summary>
    /// Every setting, each made by the migrations with no value.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [OllamaEndpoint];

    /// <summary>
    /// The setting's name, one of <see cref="All"/>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Its value, null until an admin sets one.
    /// </summary>
    public string? Value { get; set; }
}
