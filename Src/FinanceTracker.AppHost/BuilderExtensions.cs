using Microsoft.Extensions.Configuration;

namespace FinanceTracker.AppHost;

internal static class BuilderExtensions
{
    extension<T>(IResourceBuilder<T> resourceBuilder) where T : IResourceWithEnvironment
    {
        public IResourceBuilder<T> AddSentry(IConfigurationSection sentryConfig)
        {
            resourceBuilder.WithEnvironment("SENTRY_DSN", sentryConfig["Dsn"]);
            resourceBuilder.WithEnvironment("SENTRY_DEBUG", sentryConfig["Debug"]);
            resourceBuilder.WithEnvironment("SENTRY_AUTO_SESSION_TRACKING", sentryConfig["AutoSessionTracking"]);
            resourceBuilder.WithEnvironment("SENTRY_TRACES_SAMPLE_RATE", sentryConfig["TracesSampleRate"]);
            resourceBuilder.WithEnvironment("SENTRY_PROFILES_SAMPLE_RATE", sentryConfig["ProfilesSampleRate"]);
            resourceBuilder.WithEnvironment("SENTRY_RELEASE", sentryConfig["Release"]);
            resourceBuilder.WithEnvironment("SENTRY_CAPTURE_FAILED_REQUESTS", sentryConfig["CaptureFailedRequests"]);

            return resourceBuilder;
        }

        public IResourceBuilder<T> AddOpenBanking(IConfigurationSection openBankingConfig)
        {
            resourceBuilder.WithEnvironment("OPEN_BANKING_TRUELAYER_BASE_AUTH_URL",
                openBankingConfig["TrueLayer:BaseAuthUrl"]);
            resourceBuilder.WithEnvironment("OPEN_BANKING_TRUELAYER_BASE_DATA_URL",
                openBankingConfig["TrueLayer:BaseDataUrl"]);
            resourceBuilder.WithEnvironment("OPEN_BANKING_TRUELAYER_AUTH_REDIRECT_URL",
                openBankingConfig["TrueLayer:AuthRedirectUrl"]);
            resourceBuilder.WithEnvironment("OPEN_BANKING_TRUELAYER_CLIENT_ID",
                openBankingConfig["TrueLayer:ClientId"]);
            resourceBuilder.WithEnvironment("OPEN_BANKING_TRUELAYER_CLIENT_SECRET",
                openBankingConfig["TrueLayer:ClientSecret"]);
            return resourceBuilder;
        }

        public IResourceBuilder<T> AddEncryption(IConfigurationSection encryptionConfig)
        {



            resourceBuilder.WithEnvironment("ENCRYPTION_KEY",
                encryptionConfig["SymmetricKey"]);
            resourceBuilder.WithEnvironment("ENCRYPTION_SALT",
                encryptionConfig["SymmetricSalt"]);
            resourceBuilder.WithEnvironment("ENCRYPTION_ITERATIONS",
                encryptionConfig["Iterations"]);
            return resourceBuilder;
        }
    }
}
