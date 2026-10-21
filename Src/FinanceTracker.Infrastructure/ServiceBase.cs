using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure;

public class ServiceBase<TLogger>
{
    private readonly ICurrentUser _currentUser;

    protected IDbContextFactory<FinanceTrackerContext> FinanceTrackerContextFactory { get; }
    protected ILogger<TLogger> Logger { get; }

    protected ServiceBase(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<TLogger> logger)
    {
        _currentUser = currentUser;
        FinanceTrackerContextFactory = financeTrackerContextFactory;
        Logger = logger;
    }

    /// <summary>
    /// The user this service acts for, see <see cref="ICurrentUser"/>.
    /// </summary>
    protected Guid UserId => _currentUser.UserId;
}
