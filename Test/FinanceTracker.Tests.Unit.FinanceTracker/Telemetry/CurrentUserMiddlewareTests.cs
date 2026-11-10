using System.Diagnostics;
using System.Security.Claims;
using FinanceTracker.Infrastructure;
using FinanceTracker.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Telemetry;

[NotInParallel(nameof(CurrentUserMiddlewareTests))]
public class CurrentUserMiddlewareTests : IDisposable
{
    private static readonly ActivitySource Source = new(nameof(CurrentUserMiddlewareTests));

    private readonly ActivityListener _listener = new()
    {
        ShouldListenTo = static x => x.Name == nameof(CurrentUserMiddlewareTests),
        Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
    };

    private readonly ILogger<CurrentUserMiddleware> _logger = Substitute.For<ILogger<CurrentUserMiddleware>>();
    private readonly CurrentUser _currentUser = new();

    public CurrentUserMiddlewareTests() => ActivitySource.AddActivityListener(_listener);

    public void Dispose()
    {
        _listener.Dispose();
        GC.SuppressFinalize(this);
    }

    [Test]
    public async Task Invoke_SignedIn_ActsAsThemAndTagsTheSpanAndLogs()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        bool ranNext = false;
        CurrentUserMiddleware subject = new(_ =>
        {
            ranNext = true;
            return Task.CompletedTask;
        }, _logger);
        using Activity? request = Source.StartActivity("request");

        // Act
        await subject.InvokeAsync(Context(userId), _currentUser);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ranNext).IsTrue();
            await Assert.That(_currentUser.UserId).IsEqualTo(userId);
            await Assert.That(request!.GetTagItem("user.id")).IsEqualTo(userId.ToString());
            _logger.Received(1).BeginScope(Arg.Is<KeyValuePair<string, object?>[]>(x =>
                x.Single().Key == "user.id" && Equals(x.Single().Value, userId.ToString())));
        }
    }

    [Test]
    public async Task Invoke_SignedOut_TagsNothing()
    {
        // Arrange
        CurrentUserMiddleware subject = new(static _ => Task.CompletedTask, _logger);
        using Activity? request = Source.StartActivity("request");

        // Act
        await subject.InvokeAsync(new DefaultHttpContext(), _currentUser);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(_currentUser.UserIdIfSet).IsNull();
            await Assert.That(request!.GetTagItem("user.id")).IsNull();
            _logger.DidNotReceiveWithAnyArgs().BeginScope<object>(default!);
        }
    }

    private static DefaultHttpContext Context(Guid userId) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            "Test"))
    };
}
