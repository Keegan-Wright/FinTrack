namespace FinanceTracker.Contracts.Automation;

/// <summary>
/// Where an <see cref="AutomationRun"/> has got to.
/// </summary>
public enum AutomationRunStatus
{
    /// <summary>
    /// Due, or queued, and not started yet.
    /// </summary>
    Waiting,

    /// <summary>
    /// Running for the first time.
    /// </summary>
    Running,

    /// <summary>
    /// Failed at least once and running, or queued to run, again.
    /// </summary>
    Retrying,

    /// <summary>
    /// Finished without an error.
    /// </summary>
    Done,

    /// <summary>
    /// Failed on its last try.
    /// </summary>
    Failed,

    /// <summary>
    /// Stopped before it finished.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Not run, e.g. because the previous run was still going.
    /// </summary>
    Skipped
}
