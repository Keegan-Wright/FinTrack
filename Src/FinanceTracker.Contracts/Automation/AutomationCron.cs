namespace FinanceTracker.Contracts.Automation;

public record AutomationCron(
    Guid Id,
    string Function,
    string Description,
    string Expression,
    int Retries,
    int[]? RetryIntervals,
    bool IsEnabled);
