using FinanceTracker.Contracts.Diagnostics;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Diagnostics;

/// <summary>
/// Logs what the app in the browser reports as an Error, with the user and page, in the request that brought it so
/// the trace shows it too. Nothing is stored.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IClientErrorsService>]
public sealed class ClientErrorsService : ServiceBase<ClientErrorsService>, IClientErrorsService
{
    public ClientErrorsService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<ClientErrorsService> logger)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
    }

    public Task ReportAsync(ClientError error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(error);
        Logger.LogBrowserError(UserId, error.Page, error.Category, error.ExceptionType, error.Message,
            error.StackTrace);
        return Task.CompletedTask;
    }
}
