namespace FinanceTracker.Services;

public interface IServiceBase
{
    protected internal Guid AutomationInstanceUserId { set; }

    protected internal void SetAutomationInstanceUserId(Guid value) => AutomationInstanceUserId = value;
}
