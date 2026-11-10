using System.Diagnostics;
using FinanceTracker.Extensions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Telemetry;

namespace FinanceTracker.Middleware;

/// <summary>
/// Sets the request's <see cref="CurrentUser"/>, after authentication so the user is known. When someone's signed in,
/// tags the request's span with them and the request's logs too.
/// </summary>
public sealed class CurrentUserMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CurrentUserMiddleware> _logger;

    public CurrentUserMiddleware(RequestDelegate next, ILogger<CurrentUserMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, CurrentUser currentUser)
    {
        currentUser.Set(context.User);
        if (currentUser.UserIdIfSet is not { } userId)
        {
            await _next(context);
            return;
        }

        Activity.Current?.SetTag(TelemetryTags.UserId, userId.ToString());
        using (_logger.BeginUserScope(userId))
        {
            await _next(context);
        }
    }
}
