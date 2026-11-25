namespace FinanceTracker.Ai.Contracts;

/// <summary>
/// What an Ollama endpoint has to look like, checked the same in the browser and on the server.
/// </summary>
public static class OllamaEndpoint
{
    /// <summary>
    /// What to tell an admin whose address isn't one.
    /// </summary>
    public const string InvalidMessage = "Enter a full http or https address, e.g. http://ollama:11434.";

    /// <summary>
    /// Whether <paramref name="endpoint"/> is an address Ollama can be reached on, absolute http or https.
    /// </summary>
    /// <param name="endpoint">The address.</param>
    public static bool IsValid(string endpoint) =>
        Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out Uri? uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
