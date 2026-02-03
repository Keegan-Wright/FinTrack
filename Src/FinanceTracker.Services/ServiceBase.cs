using System.Security.Claims;
using FinanceTracker.Data;

namespace FinanceTracker.Services;

public class ServiceBase
{ 
    internal readonly ClaimsPrincipal _user;
    internal readonly FinanceTrackerContext _financeTrackerContext;

    public ServiceBase(ClaimsPrincipal user, FinanceTrackerContext financeTrackerContext)
    {
        _user = user;
        _financeTrackerContext = financeTrackerContext;
    }
    
    protected string Username => _user.Identity.Name;
    protected Guid UserId => Guid.Parse(_user.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Sid).Value);
}