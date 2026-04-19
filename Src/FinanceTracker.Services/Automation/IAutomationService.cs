using FinanceTracker.Models.Request.Automation;
using FinanceTracker.Models.Response.Automation;

namespace FinanceTracker.Services.Automation;

public interface IAutomationService
{
    IAsyncEnumerable<AutomationCronResponse> GetJobs(CancellationToken cancellationToken);
    Task<bool> UpdateJobSettings(CronJobUpdateRequest request, CancellationToken cancellationToken);
}
