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
    /// <param name="serviceScopeFactory">Scopes with <see cref="AddTransferMatching"/>, which sharing runs for both
    /// people when shares change.</param>
    public static SharingService Build(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        IServiceScopeFactory serviceScopeFactory)
    {
        ILogger<SharingService> logger = NullLogger<SharingService>.Instance;
        TimeProvider timeProvider = TimeProvider.System;

        SharingEffects sharingEffects = new(currentUser, financeTrackerContextFactory, logger, serviceScopeFactory);
        SharingChoices sharingChoices = new(currentUser, financeTrackerContextFactory, logger, timeProvider,
            sharingEffects);
        SharingJointAccounts sharingJointAccounts = new(currentUser, financeTrackerContextFactory, logger,
            timeProvider, sharingEffects, sharingChoices);
        SharingLinks sharingLinks = new(currentUser, financeTrackerContextFactory, logger, timeProvider,
            sharingEffects, sharingJointAccounts);
        SharingViews sharingViews = new(currentUser, financeTrackerContextFactory, logger);

        return new SharingService(sharingLinks, sharingChoices, sharingJointAccounts, sharingViews);
    }


    extension(IServiceCollection services)
    {
        /// <summary>
        /// Transfer matching and rule application acting as each scope's user, which sharing runs for both people
        /// when shares change.
        /// </summary>
        public IServiceCollection AddTransferMatching(
            IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory) =>
            services
                .AddScoped<ITransferMatchingService>(serviceProvider => new TransferMatchingService(
                    serviceProvider.GetRequiredService<ICurrentUser>(), financeTrackerContextFactory,
                    NullLogger<TransferMatchingService>.Instance, TimeProvider.System, TestMetrics.Unobserved))
                .AddScoped<IRuleApplicationService>(serviceProvider => new RuleApplicationService(
                    serviceProvider.GetRequiredService<ICurrentUser>(), financeTrackerContextFactory,
                    NullLogger<RuleApplicationService>.Instance,
                    serviceProvider.GetRequiredService<ITransferMatchingService>(),
                    new AccountScope(serviceProvider.GetRequiredService<ICurrentUser>()), TimeProvider.System,
                    TestMetrics.Unobserved));
    }
}
