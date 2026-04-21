using FinanceTracker.Models.Request.Automation;
using FinanceTracker.Models.Response.Automation;

namespace FinanceTracker.Services.Automation;

public interface IAutomationService
{
    IAsyncEnumerable<AutomationCronResponse> GetJobsAsync(CancellationToken cancellationToken);
    Task<bool> UpdateJobSettingsAsync(CronJobUpdateRequest request, CancellationToken cancellationToken);
    Task<DateTime?> GetLastSyncTimeAsync(CancellationToken cancellationToken);
}
