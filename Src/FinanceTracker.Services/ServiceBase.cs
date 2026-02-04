using System.Security.Claims;
using FinanceTracker.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Services;

public class ServiceBase
{ 
    internal readonly ClaimsPrincipal _user;
    internal readonly IDbContextFactory<FinanceTrackerContext> _financeTrackerContextFactory;

    public ServiceBase(ClaimsPrincipal user, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory)
    {
        _user = user;
        _financeTrackerContextFactory = financeTrackerContextFactory;
    }
    
    protected string Username => _user.Identity.Name;
    protected Guid UserId => Guid.Parse(_user.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Sid).Value);
}