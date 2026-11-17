using System.Diagnostics;

namespace FinanceTracker.Telemetry;

/// <summary>
/// Helpers for starting spans.
/// </summary>
public static class ActivitySourceExtensions
{
    extension(ActivitySource source)
    {
        /// <summary>
        /// A span for work done for <paramref name="userId"/>, a child of the current one, tagged with who it's for.
        /// </summary>
        /// <param name="name">The span's name.</param>
        /// <param name="userId">Who the work is for.</param>
        public Activity? StartUserActivity(string name, Guid userId)
        {
            Activity? activity = source.StartActivity(name);
            activity?.SetTag(TelemetryTags.UserId, userId.ToString());
            return activity;
        }
    }
}
