using FinanceTracker.Contracts.Automation;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Enums;

namespace FinanceTracker.Infrastructure.Automation;

/// <summary>
/// TickerQ's stored runs as <see cref="AutomationRun"/>s.
/// </summary>
public static class TickerRunExtensions
{
    extension(CronTickerOccurrenceEntity<CronTickerEntity> occurrence)
    {
        /// <summary>
        /// A run of <paramref name="function"/>'s schedule.
        /// </summary>
        public AutomationRun ToRun(string function, TimeProvider timeProvider) =>
            new(occurrence.Id, function, occurrence.Status.ToRunStatus(occurrence.RetryCount), IsManual: false,
                timeProvider.ToLocal(occurrence.ExecutionTime),
                StartedAt(occurrence.Status, occurrence.ExecutionTime, occurrence.LockedAt, occurrence.ExecutedAt,
                    occurrence.ElapsedTime, timeProvider),
                Duration(occurrence.Status, occurrence.ElapsedTime), occurrence.RetryCount,
                occurrence.ExceptionMessage ?? occurrence.SkippedReason);
    }

    extension(TimeTickerEntity ticker)
    {
        /// <summary>
        /// A run started by hand, due when it was asked for if it has no time of its own.
        /// </summary>
        public AutomationRun ToRun(TimeProvider timeProvider) =>
            new(ticker.Id, ticker.Function, ticker.Status.ToRunStatus(ticker.RetryCount), IsManual: true,
                timeProvider.ToLocal(ticker.ExecutionTime ?? ticker.CreatedAt),
                StartedAt(ticker.Status, ticker.ExecutionTime ?? ticker.CreatedAt, ticker.LockedAt, ticker.ExecutedAt,
                    ticker.ElapsedTime, timeProvider),
                Duration(ticker.Status, ticker.ElapsedTime), ticker.RetryCount,
                ticker.ExceptionMessage ?? ticker.SkippedReason);
    }

    extension(TickerStatus status)
    {
        /// <summary>
        /// The status as the app shows it, a run that has failed before and isn't finished being a retry.
        /// </summary>
        public AutomationRunStatus ToRunStatus(int retryCount)
        {
            bool retried = retryCount > 0;

            switch (status)
            {
                case TickerStatus.Idle or TickerStatus.Queued:
                    return retried ? AutomationRunStatus.Retrying : AutomationRunStatus.Waiting;
                case TickerStatus.InProgress:
                    return retried ? AutomationRunStatus.Retrying : AutomationRunStatus.Running;
                case TickerStatus.Failed:
                    return AutomationRunStatus.Failed;
                case TickerStatus.Cancelled:
                    return AutomationRunStatus.Cancelled;
                case TickerStatus.Skipped:
                    return AutomationRunStatus.Skipped;
                default:
                    return AutomationRunStatus.Done;
            }
        }
    }

    /// <summary>
    /// When the run started. TickerQ queues and locks a scheduled run well before it's due, so being locked doesn't
    /// mean it has started, and it records when a run finished rather than when it began.
    /// </summary>
    private static DateTime? StartedAt(TickerStatus status, DateTime dueAt, DateTime? lockedAt, DateTime? executedAt,
        long elapsedMilliseconds, TimeProvider timeProvider)
    {
        switch (status)
        {
            case TickerStatus.Done or TickerStatus.DueDone or TickerStatus.Failed when executedAt is not null:
                return timeProvider.ToLocal(executedAt.Value.AddMilliseconds(-elapsedMilliseconds));
            case TickerStatus.InProgress:
                return timeProvider.ToLocal(Later(lockedAt, dueAt));
            case TickerStatus.Cancelled or TickerStatus.Skipped when executedAt is not null:
                return timeProvider.ToLocal(executedAt.Value);
            default:
                return null;
        }
    }

    // A run can't start before it's due, however early TickerQ locked it
    private static DateTime Later(DateTime? lockedAt, DateTime dueAt)
    {
        if (lockedAt is null || lockedAt < dueAt)
        {
            return dueAt;
        }

        return lockedAt.Value;
    }

    // TickerQ times each run in milliseconds, and only a finished run's time is the whole of it
    private static TimeSpan? Duration(TickerStatus status, long elapsedMilliseconds)
    {
        bool finished = status is TickerStatus.Done or TickerStatus.DueDone or TickerStatus.Failed;
        if (!finished)
        {
            return null;
        }

        return TimeSpan.FromMilliseconds(elapsedMilliseconds);
    }
}
