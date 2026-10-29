using System.Net;
using System.Threading.RateLimiting;
using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer;
using Microsoft.Extensions.Http.Resilience;

namespace FinanceTracker.Extensions;

/// <summary>
/// Registers the TrueLayer intergration its settings and the two HTTP clients <see cref="TrueLayerOpenBankingApiService"/> calls it
/// through.
/// </summary>
/// <remarks>
/// Every HTTP client already gets the standard resilience handler from the service defaults. These clients tune it for
/// TrueLayer, whose rate limiter refuses bursts with a 429 and no <c>Retry-After</c>:
/// <list type="bullet">
/// <item>Both wait longer before retrying a 429, see <see cref="TooManyRequestsDelay"/>.</item>
/// <item>The data client, which a sync calls, is throttled by <see cref="CreateDataRateLimiter"/>. The standard client,
/// for sign-ins and logos, isn't, so a running sync doesn't hold them up.</item>
/// </list>
/// </remarks>
public static class OpenBankingExtensions
{
    /// <summary>
    /// The gap between retries of a 429, each retry waits one more step than the last.
    /// </summary>
    private static readonly TimeSpan TooManyRequestsBackOffStep = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Long enough for every 429 retry's wait, 15 + 30 + 45 seconds, plus the attempts themselves.
    /// </summary>
    private static readonly TimeSpan TotalRequestTimeout = TimeSpan.FromMinutes(2);

    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// TrueLayer's settings and its two HTTP clients:
        /// <see cref="TrueLayerOpenBankingApiService.StandardHttpClientName"/> and the throttled
        /// <see cref="TrueLayerOpenBankingApiService.DataHttpClientName"/>, limited by
        /// <see cref="TrueLayerThrottlingOptions"/> from <see cref="TrueLayerThrottlingOptions.SectionName"/>.
        /// </summary>
        public WebApplicationBuilder AddOpenBanking()
        {
            builder.Services.AddSingleton(ReadTrueLayerConfiguration(builder.Configuration));

            TrueLayerThrottlingOptions throttling =
                builder.Configuration.GetSection(TrueLayerThrottlingOptions.SectionName)
                    .Get<TrueLayerThrottlingOptions>() ?? new TrueLayerThrottlingOptions();

            // One limiter for the whole integration, every user's connections share TrueLayer's limit on the client id.
            // Built at startup for up front errors.
            RateLimiter dataRateLimiter = CreateDataRateLimiter(throttling);

            AddTrueLayerClient(builder.Services, TrueLayerOpenBankingApiService.StandardHttpClientName);
            AddTrueLayerClient(builder.Services, TrueLayerOpenBankingApiService.DataHttpClientName);
            ThrottleClient(builder.Services, TrueLayerOpenBankingApiService.DataHttpClientName, dataRateLimiter);

            return builder;
        }
    }

    /// <summary>
    /// Lets <see cref="TrueLayerThrottlingOptions.MaxConcurrentCalls"/> calls be in flight and
    /// <see cref="TrueLayerThrottlingOptions.MaxCallsPerSecond"/> start each second. Calls over either limit queue in
    /// order rather than being refused.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Either limit isn't above zero.</exception>
    public static RateLimiter CreateDataRateLimiter(TrueLayerThrottlingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxConcurrentCalls);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxCallsPerSecond);

        ConcurrencyLimiter concurrency = new(new ConcurrencyLimiterOptions
        {
            PermitLimit = options.MaxConcurrentCalls,
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });

        TokenBucketRateLimiter perSecond = new(new TokenBucketRateLimiterOptions
        {
            TokenLimit = options.MaxCallsPerSecond,
            TokensPerPeriod = options.MaxCallsPerSecond,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            AutoReplenishment = true,
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });

        return RateLimiter.CreateChained(concurrency, perSecond);
    }

    /// <summary>
    /// How long to wait before retrying a refused (429) TrueLayer request: 15 seconds, then 30, then 45. Null for any
    /// other failure, which keeps the standard delay.
    /// </summary>
    /// <param name="attemptNumber">The retry about to happen, from 0.</param>
    public static TimeSpan? TooManyRequestsDelay(HttpStatusCode? statusCode, int attemptNumber)
    {
        if (statusCode != HttpStatusCode.TooManyRequests)
        {
            return null;
        }

        return TooManyRequestsBackOffStep * (attemptNumber + 1);
    }

    private static TrueLayerOpenBankingConfiguration ReadTrueLayerConfiguration(IConfiguration configuration) => new()
    {
        BaseAuthUrl = configuration.GetValue<Uri>(FinTrackEnvironment.TrueLayerBaseAuthUrl)!,
        BaseDataUrl = configuration.GetValue<Uri>(FinTrackEnvironment.TrueLayerBaseDataUrl)!,
        AuthRedirectUrl = configuration.GetValue<Uri>(FinTrackEnvironment.TrueLayerAuthRedirectUrl)!,
        ClientId = configuration.GetValue<string>(FinTrackEnvironment.TrueLayerClientId)!,
        ClientSecret = configuration.GetValue<string>(FinTrackEnvironment.TrueLayerClientSecret)!,
        PublicIpAddress = configuration.GetValue<string>(FinTrackEnvironment.PublicIpAddress) ?? string.Empty
    };

    /// <summary>
    /// Registers a TrueLayer client whose standard resilience handler waits <see cref="TooManyRequestsDelay"/> before
    /// retrying a 429, rather than the few seconds it waits by default.
    /// </summary>
    private static void AddTrueLayerClient(IServiceCollection services, string clientName)
    {
        services.AddHttpClient(clientName);
        services.Configure<HttpStandardResilienceOptions>(StandardResilienceOptionsName(clientName), options =>
        {
            options.Retry.DelayGenerator = static arguments => ValueTask.FromResult(
                TooManyRequestsDelay(arguments.Outcome.Result?.StatusCode, arguments.AttemptNumber));
            options.TotalRequestTimeout.Timeout = TotalRequestTimeout;
        });
    }

    /// <summary>
    /// Makes every request on the client wait for <paramref name="rateLimiter"/>. The standard handler limits before it
    /// starts the total request timeout, so time spent queuing doesn't count against it.
    /// </summary>
    private static void ThrottleClient(IServiceCollection services, string clientName, RateLimiter rateLimiter) =>
        services.Configure<HttpStandardResilienceOptions>(StandardResilienceOptionsName(clientName),
            options => options.RateLimiter.RateLimiter = arguments =>
                rateLimiter.AcquireAsync(1, arguments.Context.CancellationToken));

    /// <summary>
    /// The name the standard resilience handler's options go by for <paramref name="clientName"/>.
    /// </summary>
    private static string StandardResilienceOptionsName(string clientName) => $"{clientName}-standard";
}
