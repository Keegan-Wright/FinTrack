namespace FinanceTracker.Models.Response.Automation;

public record AutomationCronResponse(
    Guid Id,
    string Function,
    string Description,
    string Expression,
    int Retries,
    int[]? RetryIntervals,
    bool IsEnabled);
