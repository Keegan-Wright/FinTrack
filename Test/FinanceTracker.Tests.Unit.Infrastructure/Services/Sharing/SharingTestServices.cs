using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Infrastructure.Rules;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Sharing;

public static class SharingTestServices
{
    /// <summary>
    /// The sharing service acting as <paramref name="currentUser"/>, with its parts built as the app's container
    /// would.
    /// </summary>
    /// <param name="scopeFactory">Scopes with <see cref="AddTransferMatching"/>, which sharing runs for both people
    /// when shares change.</param>
    public static SharingService Build(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> contextFactory, IServiceScopeFactory scopeFactory)
    {
        ILogger<SharingService> logger = NullLogger<SharingService>.Instance;
        TimeProvider timeProvider = TimeProvider.System;

        SharingEffects effects = new(currentUser, contextFactory, logger, scopeFactory);
        SharingChoices choices = new(currentUser, contextFactory, logger, timeProvider, effects);
        SharingJointAccounts jointAccounts = new(currentUser, contextFactory, logger, timeProvider, effects, choices);
        SharingLinks links = new(currentUser, contextFactory, logger, timeProvider, effects, jointAccounts);
        SharingViews views = new(currentUser, contextFactory, logger);

        return new SharingService(links, choices, jointAccounts, views);
    }

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
