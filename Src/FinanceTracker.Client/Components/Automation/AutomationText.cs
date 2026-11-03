using System.Globalization;
using System.Text;
using FinanceTracker.Contracts.Automation;
using MudBlazor;

namespace FinanceTracker.Client.Components.Automation;

/// <summary>
/// The words and colours the automation page uses for jobs and their runs.
/// </summary>
public static class AutomationText
{
    /// <summary>
    /// A job's function as words, e.g. <c>SyncAllOpenBankingDetailsAsync</c> is <c>Sync all open banking details</c>.
    /// </summary>
    public static string JobName(string function)
    {
        string name = function.EndsWith("Async", StringComparison.Ordinal) ? function[..^"Async".Length] : function;
        if (name.Length == 0)
        {
            return function;
        }

        StringBuilder words = new(name.Length + 8);
        words.Append(name[0]);
        for (int i = 1; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]))
            {
                words.Append(' ').Append(char.ToLowerInvariant(name[i]));
            }
            else
            {
                words.Append(name[i]);
            }
        }

        return words.ToString();
    }

    /// <summary>
    /// Whether the job runs on its schedule, is paused, or only runs by hand.
    /// </summary>
    public static string State(AutomationJobSummary job)
    {
        if (job.Schedule is null)
        {
            return "By hand";
        }

        return job.Schedule.IsEnabled ? "Enabled" : "Paused";
    }

    public static Color StateColor(AutomationJobSummary job)
    {
        if (job.Schedule is { IsEnabled: true })
        {
            return Color.Success;
        }

        return Color.Default;
    }

    public static string NextRun(AutomationJobSummary job)
    {
        if (job.Schedule is null)
        {
            return "Only runs when asked to";
        }

        if (job.NextRunAt is null)
        {
            return "Not while it's paused";
        }

        return When(job.NextRunAt.Value);
    }

    public static string LastRun(AutomationJobSummary job)
    {
        if (job.LastRun is null)
        {
            return "Not run yet";
        }

        return RunWhen(job.LastRun);
    }

    public static string SuccessRate(AutomationJobSummary job)
    {
        if (job.SuccessRate is null)
        {
            return "No finished runs yet";
        }

        if (job.RecentRuns == 1)
        {
            return $"{job.SuccessRate}% of the last run";
        }

        return $"{job.SuccessRate}% of the last {job.RecentRuns} runs";
    }

    /// <summary>
    /// Amber once a recent run has failed, red when most have.
    /// </summary>
    public static Color SuccessRateColor(AutomationJobSummary job)
    {
        if (job.SuccessRate is null or 100)
        {
            return Color.Default;
        }

        return job.SuccessRate >= 50 ? Color.Warning : Color.Error;
    }

    public static string StatusName(AutomationRunStatus status) => status.ToString();

    public static Color StatusColor(AutomationRunStatus status)
    {
        switch (status)
        {
            case AutomationRunStatus.Done:
                return Color.Success;
            case AutomationRunStatus.Failed:
                return Color.Error;
            case AutomationRunStatus.Retrying:
                return Color.Warning;
            case AutomationRunStatus.Running:
                return Color.Info;
            default:
                return Color.Default;
        }
    }

    /// <summary>
    /// When the run started and how long it took once it's finished, or when it's due if it hasn't started.
    /// </summary>
    public static string RunWhen(AutomationRun run)
    {
        if (run.StartedAt is null)
        {
            return $"Due {When(run.DueAt)}";
        }

        string when = When(run.StartedAt.Value);
        if (run.Duration is null)
        {
            return when;
        }

        return $"{when}, took {Duration(run.Duration.Value)}";
    }

    /// <summary>
    /// How many times a run was retried, or <see langword="null"/> if it wasn't.
    /// </summary>
    public static string? Retries(AutomationRun run)
    {
        if (run.RetryCount == 0)
        {
            return null;
        }

        return run.RetryCount == 1 ? "Retried once" : $"Retried {run.RetryCount} times";
    }

    /// <summary>
    /// A time on the page, e.g. <c>3 Nov 14:05</c>.
    /// </summary>
    public static string When(DateTime value) => value.ToString("d MMM HH:mm", CultureInfo.CurrentCulture);

    /// <summary>
    /// A run's length to the nearest useful unit, e.g. <c>4.5s</c>, <c>2m 10s</c> or <c>1h 5m</c>.
    /// </summary>
    public static string Duration(TimeSpan duration)
    {
        if (duration < TimeSpan.FromMinutes(1))
        {
            return $"{duration.TotalSeconds.ToString("0.#", CultureInfo.CurrentCulture)}s";
        }

        if (duration < TimeSpan.FromHours(1))
        {
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        }

        return $"{(int)duration.TotalHours}h {duration.Minutes}m";
    }
}
