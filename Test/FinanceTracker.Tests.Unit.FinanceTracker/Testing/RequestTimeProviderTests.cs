using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Extensions;
using FinanceTracker.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Testing;

public class RequestTimeProviderTests
{
    private static readonly TimeSpan Slack = TimeSpan.FromSeconds(5);

    [Test]
    public async Task GetUtcNow_TheRequestMovesTheClock_IsThatFarFromNow()
    {
        // Arrange, a day back
        RequestTimeProvider clock = ClockFor(cookie: "-86400");

        // Act
        DateTimeOffset now = clock.GetUtcNow();

        // Assert
        await Assert.That(now).IsEqualTo(DateTimeOffset.UtcNow.AddDays(-1)).Within(Slack);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("tomorrow")]
    [Arguments("1.5")]
    public async Task GetUtcNow_NoUsableOffset_IsNow(string? cookie)
    {
        // Arrange
        RequestTimeProvider clock = ClockFor(cookie);

        // Act
        DateTimeOffset now = clock.GetUtcNow();

        // Assert
        await Assert.That(now).IsEqualTo(DateTimeOffset.UtcNow).Within(Slack);
    }

    [Test]
    public async Task GetUtcNow_OutsideARequest_IsNow()
    {
        // Arrange, e.g. a background job
        RequestTimeProvider clock = new(new HttpContextAccessor());

        // Act
        DateTimeOffset now = clock.GetUtcNow();

        // Assert
        await Assert.That(now).IsEqualTo(DateTimeOffset.UtcNow).Within(Slack);
    }

    [Test]
    [Arguments(null, false)]
    [Arguments("false", false)]
    [Arguments("true", true)]
    public async Task AddFinTrackClock_OnlyWithTheTestClockOn_LetsARequestMoveIt(string? setting, bool movable)
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        if (setting is not null)
        {
            builder.Configuration[FinTrackEnvironment.TestClock] = setting;
        }

        // Act
        builder.AddFinTrackClock();

        // Assert
        await using ServiceProvider services = builder.Services.BuildServiceProvider();
        await Assert.That(services.GetRequiredService<TimeProvider>() is RequestTimeProvider).IsEqualTo(movable);
    }

    private static RequestTimeProvider ClockFor(string? cookie)
    {
        DefaultHttpContext context = new();
        if (cookie is not null)
        {
            context.Request.Headers.Cookie = $"{TestClock.Cookie}={cookie}";
        }

        return new RequestTimeProvider(new HttpContextAccessor { HttpContext = context });
    }
}
