using FinanceTracker.Contracts.Automation;

namespace FinanceTracker.Infrastructure.Automation;

public interface IAutomationService
{
    IAsyncEnumerable<AutomationCronResponse> GetJobsAsync(CancellationToken cancellationToken);
    Task<bool> UpdateJobSettingsAsync(CronJobUpdateRequest request, CancellationToken cancellationToken);
    Task<DateTime?> GetLastSyncTimeAsync(CancellationToken cancellationToken);
}
