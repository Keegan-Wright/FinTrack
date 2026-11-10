namespace FinanceTracker.Extensions;

public static class UserLogScopeExtensions
{
    extension(ILogger logger)
    {
        /// <summary>
        /// Tags every log written until it's disposed, by any logger, with the user the work is for. Logs already carry the trace they were written in.
        /// </summary>
        public IDisposable? BeginUserScope(Guid userId) =>
            logger.BeginScope(new[] { new KeyValuePair<string, object?>(Telemetry.UserIdTag, userId.ToString()) });
    }
}
