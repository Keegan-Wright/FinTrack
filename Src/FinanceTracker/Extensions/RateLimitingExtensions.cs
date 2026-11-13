using System.Security.Claims;
using System.Threading.RateLimiting;
using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Middleware;

namespace FinanceTracker.Extensions;

public static class RateLimitingExtensions
{
    private const int DefaultSignInsPerMinute = 10;
    private const int DefaultInvitesPerHour = 20;

    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// Limits how often the requests <see cref="RateLimitedActions"/> picks out can be made, as
        /// <see cref="FinTrackEnvironment.SignInsPerMinute"/> and <see cref="FinTrackEnvironment.InvitesPerHour"/> set.
        /// Over the limit, the request is answered too many requests. Anything else isn't limited.
        /// </summary>
        public WebApplicationBuilder AddFinTrackRateLimiting()
        {
            int signIns = builder.Configuration.GetValue(FinTrackEnvironment.SignInsPerMinute,
                DefaultSignInsPerMinute);
            int invites = builder.Configuration.GetValue(FinTrackEnvironment.InvitesPerHour, DefaultInvitesPerHour);

            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    Partition(context, signIns, invites));
            });
            return builder;
        }
    }

    /// <summary>
    /// The limit <paramref name="context"/> counts towards. Sign ins per address a minute, invites per user an hour,
    /// or none.
    /// </summary>
    private static RateLimitPartition<string> Partition(HttpContext context, int signIns, int invites)
    {
        RateLimitedAction? action = RateLimitedActions.Of(context.Request.Method, context.Request.Path);
        string address = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (action == RateLimitedAction.SignIn)
        {
            return RateLimitPartition.GetFixedWindowLimiter($"sign-in:{address}", _ =>
                Window(signIns, TimeSpan.FromMinutes(1)));
        }

        if (action == RateLimitedAction.Invite)
        {
            string user = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? address;
            return RateLimitPartition.GetFixedWindowLimiter($"invite:{user}", _ =>
                Window(invites, TimeSpan.FromHours(1)));
        }

        return RateLimitPartition.GetNoLimiter(string.Empty);
    }

    private static FixedWindowRateLimiterOptions Window(int permits, TimeSpan window) => new()
    {
        PermitLimit = permits,
        Window = window,
        QueueLimit = 0
    };
}
