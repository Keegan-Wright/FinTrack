using System.Security.Claims;
using FinanceTracker.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Services;

public class ServiceBase : IServiceBase
{
    protected IDbContextFactory<FinanceTrackerContext> FinanceTrackerContextFactory { get; }

    private ClaimsPrincipal? User { get; }

    protected ServiceBase(ClaimsPrincipal? user, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory)
    {
        User = user;
        FinanceTrackerContextFactory = financeTrackerContextFactory;
    }

    protected Guid UserId => User != null
        ? Guid.Parse(User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value!)
        : AutomationInstanceUserId;


    public Guid AutomationInstanceUserId { get; set; }
}
