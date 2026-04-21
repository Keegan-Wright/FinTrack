namespace FinanceTracker.Models.Request.Automation;

public class CronJobUpdateRequest()
{
    public Guid Id { get; set; }
    public string Description { get; set; }
    public required string Expression { get; set; }
    public int Retries { get; set; }
    public int[] RetryIntervals { get; set; }
    public required bool IsEnabled { get; set; }


}
