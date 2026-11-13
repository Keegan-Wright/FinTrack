using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Infrastructure.Rules;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Sharing;

public static class SharingTestServices
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Transfer matching and rule application acting as each scope's user, which sharing runs for both people
        /// when shares change.
        /// </summary>
        public IServiceCollection AddTransferMatching(IDbContextFactory<FinanceTrackerContext> contextFactory) =>
            services
                .AddScoped<ITransferMatchingService>(sp => new TransferMatchingService(
                    sp.GetRequiredService<ICurrentUser>(), contextFactory, NullLogger<TransferMatchingService>.Instance,
                    TimeProvider.System, TestMetrics.Unobserved))
                .AddScoped<IRuleApplicationService>(sp => new RuleApplicationService(
                    sp.GetRequiredService<ICurrentUser>(), contextFactory, NullLogger<RuleApplicationService>.Instance,
                    sp.GetRequiredService<ITransferMatchingService>(), new AccountScope(sp.GetRequiredService<ICurrentUser>()),
                    TimeProvider.System, TestMetrics.Unobserved));
    }
}
