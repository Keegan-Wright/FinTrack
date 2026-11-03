namespace FinanceTracker.Contracts.Automation;

/// <summary>
/// The job is already running or queued to, so it isn't started again, e.g. a second sync of every user's banks.
/// </summary>
public sealed class JobAlreadyRunningException : Exception
{
    /// <param name="function">The job, see <see cref="AutomationCron.Function"/>.</param>
    public JobAlreadyRunningException(string function) : base($"{function} is already running")
    {
        Function = function;
    }

    /// <summary>
    /// The job that's running.
    /// </summary>
    public string Function { get; }
}
