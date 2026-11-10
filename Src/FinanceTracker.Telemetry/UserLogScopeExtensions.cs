using Microsoft.Extensions.Logging;

namespace FinanceTracker.Telemetry;

public static class UserLogScopeExtensions
{
    extension(ILogger logger)
    {
        /// <summary>
        /// Tags every log written until it's disposed, by any logger, with the user the work is for, so Loki can
        /// pick out one user's logs. Logs already carry the trace they were written in.
        /// </summary>
        public IDisposable? BeginUserScope(Guid userId) =>
            logger.BeginScope(new[] { new KeyValuePair<string, object?>(TelemetryTags.UserId, userId.ToString()) });
    }
}
