namespace FinanceTracker.Contracts.Automation;

/// <summary>
/// One page of a job's runs, newest first.
/// </summary>
/// <param name="Runs">The runs on this page.</param>
/// <param name="Total">How many runs the job has in all.</param>
public record AutomationRunPage(IReadOnlyList<AutomationRun> Runs, int Total);
