using System.Net;
using System.Threading.RateLimiting;
using FinanceTracker.Extensions;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

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
    public async Task AddOpenBanking_BothClientsWaitLongerOnTooManyRequests(string clientName)
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Act
        builder.AddOpenBanking();

        // Assert
        await using ServiceProvider services = builder.Services.BuildServiceProvider();
        HttpStandardResilienceOptions options = services
            .GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>().Get($"{clientName}-standard");
        bool waitsLonger = options.Retry.DelayGenerator is not null;
        using (Assert.Multiple())
        {
            await Assert.That(waitsLonger).IsTrue();
            await Assert.That(options.TotalRequestTimeout.Timeout).IsEqualTo(TimeSpan.FromMinutes(2));
        }
    }

    [Test]
    public async Task AddOpenBanking_ThrottlesOnlyTheDataClient()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Act
        builder.AddOpenBanking();

        // Assert
        await using ServiceProvider services = builder.Services.BuildServiceProvider();
        IOptionsMonitor<HttpStandardResilienceOptions> options =
            services.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>();
        bool dataClientThrottled =
            options.Get($"{TrueLayerOpenBankingApiService.DataHttpClientName}-standard").RateLimiter.RateLimiter
                is not null;
        bool standardClientThrottled =
            options.Get($"{TrueLayerOpenBankingApiService.StandardHttpClientName}-standard").RateLimiter.RateLimiter
                is not null;
        using (Assert.Multiple())
        {
            await Assert.That(dataClientThrottled).IsTrue();
            await Assert.That(standardClientThrottled).IsFalse();
        }
    }
}
