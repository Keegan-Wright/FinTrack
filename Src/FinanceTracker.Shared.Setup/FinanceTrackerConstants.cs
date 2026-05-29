namespace FinanceTracker.Shared.Setup;

public class FinanceTrackerConstants
{
    public const string RedisConnectionName = "FinTrack-Redis";
    public const string FinTrackDbConnectionName = "FinTrack-Postgres";
    public const string FinTrackDbName = "FinTrackDb";
    public const string OpenBankingHttpClientName = "OpenBankingClient";
    public const string OllamaResourceName = "ollama";
    public const string OllamaModelName = "llama3.2";
    public const string WebResourceName = "FinTrackWeb";
    public const string BackgroundBankingSyncCompleteEventName = "BackgroundBankingSyncComplete";
    public const string OllamaChatServiceKey = "chat";
    public const string OllamaEmbeddingsServiceKey = "embeddings";
}
