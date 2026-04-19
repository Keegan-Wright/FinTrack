namespace FinanceTracker.Models.Request.Automation;

public record CronJobUpdateRequest(Guid Id,
    string Description,
    string Expression,
    int Retries,
    int[] RetryIntervals,
    bool IsEnabled);
