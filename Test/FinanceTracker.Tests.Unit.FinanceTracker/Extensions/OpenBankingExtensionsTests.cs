using System.Net;
using System.Threading.RateLimiting;
using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Extensions;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Extensions;

public class OpenBankingExtensionsTests
{
    [Test]
    [Arguments(0, 15)]
    [Arguments(1, 30)]
    [Arguments(2, 45)]
    public async Task TooManyRequestsDelay_WaitsLongerEachRetry(int attemptNumber, int expectedSeconds) =>
        await Assert.That(OpenBankingExtensions.TooManyRequestsDelay(HttpStatusCode.TooManyRequests, attemptNumber))
            .IsEqualTo(TimeSpan.FromSeconds(expectedSeconds));

    [Test]
    [Arguments(HttpStatusCode.InternalServerError)]
    [Arguments(HttpStatusCode.ServiceUnavailable)]
    [Arguments(null)]
    public async Task TooManyRequestsDelay_OtherFailures_KeepTheStandardDelay(HttpStatusCode? statusCode) =>
        await Assert.That(OpenBankingExtensions.TooManyRequestsDelay(statusCode, 0)).IsNull();

    [Test]
    public async Task CreateDataRateLimiter_KeepsToTheConcurrentCallLimit()
    {
        // Arrange
        using RateLimiter limiter = OpenBankingExtensions.CreateDataRateLimiter(
            new TrueLayerThrottlingOptions { MaxConcurrentCalls = 2, MaxCallsPerSecond = 100 })!;

        // Act
        using RateLimitLease first = limiter.AttemptAcquire();
        using RateLimitLease second = limiter.AttemptAcquire();
        using RateLimitLease third = limiter.AttemptAcquire();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.IsAcquired).IsTrue();
            await Assert.That(second.IsAcquired).IsTrue();
            await Assert.That(third.IsAcquired).IsFalse();
        }
    }

    [Test]
    public async Task CreateDataRateLimiter_FinishedCallFreesItsSlot()
    {
        // Arrange
        using RateLimiter limiter = OpenBankingExtensions.CreateDataRateLimiter(
            new TrueLayerThrottlingOptions { MaxConcurrentCalls = 1, MaxCallsPerSecond = 100 })!;
        limiter.AttemptAcquire().Dispose();

        // Act
        using RateLimitLease next = limiter.AttemptAcquire();

        // Assert
        await Assert.That(next.IsAcquired).IsTrue();
    }

    [Test]
    public async Task CreateDataRateLimiter_KeepsToTheCallsPerSecondLimit()
    {
        // Arrange
        using RateLimiter limiter = OpenBankingExtensions.CreateDataRateLimiter(
            new TrueLayerThrottlingOptions { MaxConcurrentCalls = 100, MaxCallsPerSecond = 2 })!;

        // Act, each call finishes straight away so only the per second limit applies
        limiter.AttemptAcquire().Dispose();
        limiter.AttemptAcquire().Dispose();
        using RateLimitLease third = limiter.AttemptAcquire();

        // Assert
        await Assert.That(third.IsAcquired).IsFalse();
    }

    [Test]
    [Arguments(-1, 4)]
    [Arguments(2, -1)]
    public async Task CreateDataRateLimiter_NegativeLimit_Throws(int maxConcurrentCalls, int maxCallsPerSecond) =>
        await Assert.That(() => OpenBankingExtensions.CreateDataRateLimiter(new TrueLayerThrottlingOptions
        {
            MaxConcurrentCalls = maxConcurrentCalls,
            MaxCallsPerSecond = maxCallsPerSecond
        })).Throws<ArgumentOutOfRangeException>();

    [Test]
    public async Task CreateDataRateLimiter_NoConcurrentCallLimit_KeepsToTheCallsPerSecondLimit()
    {
        // Arrange
        using RateLimiter limiter = OpenBankingExtensions.CreateDataRateLimiter(
            new TrueLayerThrottlingOptions { MaxConcurrentCalls = 0, MaxCallsPerSecond = 2 })!;

        // Act, each lease is held so only a concurrent call limit would refuse the second
        using RateLimitLease first = limiter.AttemptAcquire();
        using RateLimitLease second = limiter.AttemptAcquire();
        using RateLimitLease third = limiter.AttemptAcquire();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(second.IsAcquired).IsTrue();
            await Assert.That(third.IsAcquired).IsFalse();
        }
    }

    [Test]
    public async Task CreateDataRateLimiter_NoCallsPerSecondLimit_KeepsToTheConcurrentCallLimit()
    {
        // Arrange
        using RateLimiter limiter = OpenBankingExtensions.CreateDataRateLimiter(
            new TrueLayerThrottlingOptions { MaxConcurrentCalls = 1, MaxCallsPerSecond = 0 })!;

        // Act, each call finishes straight away so only a per second limit would refuse them
        limiter.AttemptAcquire().Dispose();
        limiter.AttemptAcquire().Dispose();
        using RateLimitLease held = limiter.AttemptAcquire();
        using RateLimitLease overLimit = limiter.AttemptAcquire();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(held.IsAcquired).IsTrue();
            await Assert.That(overLimit.IsAcquired).IsFalse();
        }
    }

    [Test]
    public async Task CreateDataRateLimiter_NoLimits_ThrottlesNothing() =>
        await Assert.That(OpenBankingExtensions.CreateDataRateLimiter(
            new TrueLayerThrottlingOptions { MaxConcurrentCalls = 0, MaxCallsPerSecond = 0 })).IsNull();

    [Test]
    [Arguments(TrueLayerOpenBankingApiService.StandardHttpClientName)]
    [Arguments(TrueLayerOpenBankingApiService.DataHttpClientName)]
    public async Task AddOpenBanking_BothClientsWaitLongerBeforeRetryingTooManyRequests(string clientName)
    {
        // Arrange
        StubTrueLayerHandler trueLayer = new(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
        await using ServiceProvider services = BuildServices(clientName, trueLayer);
        using HttpClient client = services.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);
        using CancellationTokenSource cancellation = new();

        // Act, the standard handler's own retry would come round within a few seconds
        Task<HttpResponseMessage> request = client.GetAsync(TrueLayerUrl, cancellation.Token);
        await WaitForCallsAsync(trueLayer, 1);
        await Task.Delay(TimeSpan.FromSeconds(3));
        int callsBeforeTheWaitEnds = trueLayer.Calls;
        await cancellation.CancelAsync();

        // Assert
        await Assert.That(callsBeforeTheWaitEnds).IsEqualTo(1);
        await Assert.That(async () => await request).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task AddOpenBanking_DataClient_KeepsToTheConcurrentCallLimit()
    {
        // Arrange, the default limit is 2 calls at once
        TaskCompletionSource<HttpResponseMessage> trueLayerAnswers = new();
        StubTrueLayerHandler trueLayer = new(_ => trueLayerAnswers.Task);
        await using ServiceProvider services = BuildServices(TrueLayerOpenBankingApiService.DataHttpClientName, trueLayer);
        using HttpClient client = services.GetRequiredService<IHttpClientFactory>()
            .CreateClient(TrueLayerOpenBankingApiService.DataHttpClientName);

        // Act
        Task<HttpResponseMessage>[] requests = [.. Enumerable.Range(0, 3).Select(_ => client.GetAsync(TrueLayerUrl))];
        await WaitForCallsAsync(trueLayer, 2);
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        int callsInFlight = trueLayer.Calls;
        trueLayerAnswers.SetResult(new HttpResponseMessage(HttpStatusCode.OK));
        await Task.WhenAll(requests);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(callsInFlight).IsEqualTo(2);
            await Assert.That(trueLayer.Calls).IsEqualTo(3);
        }
    }

    [Test]
    public async Task AddOpenBanking_StandardClient_IsntThrottled()
    {
        // Arrange
        TaskCompletionSource<HttpResponseMessage> trueLayerAnswers = new();
        StubTrueLayerHandler trueLayer = new(_ => trueLayerAnswers.Task);
        await using ServiceProvider services =
            BuildServices(TrueLayerOpenBankingApiService.StandardHttpClientName, trueLayer);
        using HttpClient client = services.GetRequiredService<IHttpClientFactory>()
            .CreateClient(TrueLayerOpenBankingApiService.StandardHttpClientName);

        // Act
        Task<HttpResponseMessage>[] requests = [.. Enumerable.Range(0, 3).Select(_ => client.GetAsync(TrueLayerUrl))];
        await WaitForCallsAsync(trueLayer, 3);
        trueLayerAnswers.SetResult(new HttpResponseMessage(HttpStatusCode.OK));
        await Task.WhenAll(requests);

        // Assert
        await Assert.That(trueLayer.Calls).IsEqualTo(3);
    }

    private static readonly Uri TrueLayerUrl = new("https://truelayer.test/data/v1/me");

    /// <summary>
    /// The app's HTTP clients as it registers them, the service defaults then open banking, with
    /// <paramref name="trueLayer"/> answering <paramref name="clientName"/>'s requests.
    /// </summary>
    private static ServiceProvider BuildServices(string clientName, StubTrueLayerHandler trueLayer)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.AddServiceDefaults();
        builder.AddOpenBanking();
        builder.Services.AddHttpClient(clientName).ConfigurePrimaryHttpMessageHandler(() => trueLayer);

        return builder.Services.BuildServiceProvider();
    }

    private static async Task WaitForCallsAsync(StubTrueLayerHandler trueLayer, int calls)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

        while (trueLayer.Calls < calls)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
        }
    }
}
