using System.Security.Claims;
using FinanceTracker.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Services;

public class ServiceBase : IServiceBase
{ 
    protected internal readonly ClaimsPrincipal? _user;
    protected internal readonly IDbContextFactory<FinanceTrackerContext> _financeTrackerContextFactory;

    public ServiceBase(ClaimsPrincipal? user, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory)
    {
        _user = user;
        _financeTrackerContextFactory = financeTrackerContextFactory;
    }
    
    protected internal string Username => _user?.Identity?.Name ?? string.Empty;
    protected internal Guid UserId => _user != null  ?  Guid.Parse(_user.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value!) : AutomationInstanceUserId;


     public Guid AutomationInstanceUserId { get; set; }
}