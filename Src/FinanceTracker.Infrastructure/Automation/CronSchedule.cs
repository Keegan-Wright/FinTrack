using NCrontab;

namespace FinanceTracker.Infrastructure.Automation;

/// <summary>
/// Reads a job's cron expression the way TickerQ 6 part crons.
/// </summary>
public static class CronSchedule
{
    /// <summary>
    /// When <paramref name="expression"/> next fires after <paramref name="afterUtc"/>, in UTC as TickerQ schedules,
    /// or <see langword="null"/> if the expression can't be read.
    /// </summary>
    public static DateTime? NextAfter(string expression, DateTime afterUtc)
    {
        int parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        CrontabSchedule.ParseOptions options = new() { IncludingSeconds = parts == 6 };

        CrontabSchedule? schedule = CrontabSchedule.TryParse(expression, options);
        if (schedule is null)
        {
            return null;
        }

        return DateTime.SpecifyKind(schedule.GetNextOccurrence(afterUtc), DateTimeKind.Utc);
    }
}
