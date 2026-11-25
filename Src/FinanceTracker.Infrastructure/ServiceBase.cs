using FinanceTracker.Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure;

/// <summary>
/// What the services share, the current user, the context factory and a logger.
/// </summary>
/// <typeparam name="TLogger">The service, its logger's category.</typeparam>
public class ServiceBase<TLogger>
{
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// Makes the contexts the service reads and saves through.
    /// </summary>
    protected IDbContextFactory<FinanceTrackerContext> FinanceTrackerContextFactory { get; }

    /// <summary>
    /// The service's logger.
    /// </summary>
    protected ILogger<TLogger> Logger { get; }

    /// <param name="currentUser">The signed in user, or who the work runs as.</param>
    /// <param name="financeTrackerContextFactory">Makes the contexts the service reads and saves through.</param>
    /// <param name="logger">The service's logger.</param>
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
