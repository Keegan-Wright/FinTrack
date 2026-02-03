using Microsoft.Extensions.Configuration;

namespace FinanceTracker.AppHost;

static class BuilderExtensions
{
    public static IResourceBuilder<T> AddSentry<T>(this IResourceBuilder<T> resourceBuilder,
        IConfigurationSection sentryConfig)
        where T : IResourceWithEnvironment
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
    
    public static IResourceBuilder<T> AddOpenBanking<T>(this IResourceBuilder<T> resourceBuilder,
        IConfigurationSection openBankingConfig)
        where T : IResourceWithEnvironment
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
    
    public static IResourceBuilder<T> AddAuth<T>(this IResourceBuilder<T> resourceBuilder,
        IConfigurationSection authConfig)
        where T : IResourceWithEnvironment
    {
        resourceBuilder.WithEnvironment("AUTH_ISSUER",
            authConfig["Issuer"]);
        resourceBuilder.WithEnvironment("AUTH_AUDIENCE",
            authConfig["Audience"]);
        resourceBuilder.WithEnvironment("AUTH_SIGNING_KEY",
            authConfig["SigningKey"]);
        return resourceBuilder;
    }
}