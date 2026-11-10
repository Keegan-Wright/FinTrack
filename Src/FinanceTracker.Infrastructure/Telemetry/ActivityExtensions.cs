using System.Diagnostics;

namespace FinanceTracker.Infrastructure.Telemetry;

public static class ActivityExtensions
{
    extension(Activity? activity)
    {
        /// <summary>
        /// Marks the span failed, so the trace shows it in red with the exception on it. Nothing when there's no span,
        /// as when nothing is listening.
        /// </summary>
        public void RecordError(Exception exception)
        {
            activity?.AddException(exception);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        }
    }
}
