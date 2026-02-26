using System.Security.Claims;
using FinanceTracker.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Services;

public class ServiceBase<TLogger> : IServiceBase
{
    protected IDbContextFactory<FinanceTrackerContext> FinanceTrackerContextFactory { get; }
    protected readonly ILogger<TLogger> Logger;

    private ClaimsPrincipal? User { get; }

    protected ServiceBase(ClaimsPrincipal? user, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<TLogger> logger)
    {
        User = user;
        FinanceTrackerContextFactory = financeTrackerContextFactory;
        Logger = logger;
    }

    protected Guid UserId => User != null
        ? Guid.Parse(User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value!)
        : AutomationInstanceUserId;


    public Guid AutomationInstanceUserId { get; set; }
}
