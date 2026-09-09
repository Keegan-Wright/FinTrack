using FinanceTracker.Contracts.Automation;

namespace FinanceTracker.Infrastructure.Automation;

public interface IAutomationService
{
    IAsyncEnumerable<AutomationCron> GetJobsAsync(CancellationToken cancellationToken);
    Task<bool> UpdateJobSettingsAsync(UpdateCronJob request, CancellationToken cancellationToken);
    Task<DateTime?> GetLastSyncTimeAsync(CancellationToken cancellationToken);
}
