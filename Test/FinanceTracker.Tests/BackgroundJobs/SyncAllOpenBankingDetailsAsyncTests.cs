using System.Reflection;
using FinanceTracker.BackgroundJobs;
using FinanceTracker.Contracts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking;
using FinanceTracker.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TickerQ.Utilities.Base;
using TUnit.Core;

namespace FinanceTracker.Tests.BackgroundJobs;

public class SyncAllOpenBankingDetailsAsyncTests : ServiceTestsFixtureBase
{
    [Test]
    public async Task ExecuteAsync_WhenUsersHaveConnections_SyncsEachConnectionAsItsOwnerAndNotifiesComplete()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"sync-job-all-{Guid.NewGuid()}");
        await SeedUsersWithProvidersAsync(factory, _cancellationTokenSource.Token, primaryProviderCount: 2, secondaryProviderCount: 1);

        var openBankingService = Substitute.For<IOpenBankingService>();
        var syncService = Substitute.For<IBackgroundSyncService>();

        openBankingService.RunFunctionAsUser(Arg.Any<Guid>(), Arg.Any<Func<Task>>())
            .Returns(async callInfo =>
            {
                var function = callInfo.Arg<Func<Task>>();
                await function();
            });

        var subject = new SyncAllOpenBankingDetailsAsync(openBankingService, factory, syncService);

        await subject.ExecuteAsync(BuildTickerFunctionContext(), _cancellationTokenSource.Token);

        // One user switch per connection
        await openBankingService.Received(2)
            .RunFunctionAsUser(PrimaryUserId, Arg.Any<Func<Task>>());
        await openBankingService.Received(1)
            .RunFunctionAsUser(SecondaryUserId, Arg.Any<Func<Task>>());

        await openBankingService.Received(3)
            .SyncConnectionAsync(Arg.Any<Guid>(), SyncTypes.All, _cancellationTokenSource.Token);

        await syncService.Received(1).NotifySyncComplete();
    }

    [Test]
    public async Task ExecuteAsync_WhenProviderLoadThrows_ContinuesAndStillNotifiesComplete()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"sync-job-throws-{Guid.NewGuid()}");
        await SeedUsersWithProvidersAsync(factory, _cancellationTokenSource.Token, primaryProviderCount: 2, secondaryProviderCount: 1);

        var openBankingService = Substitute.For<IOpenBankingService>();
        var syncService = Substitute.For<IBackgroundSyncService>();

        openBankingService.RunFunctionAsUser(Arg.Any<Guid>(), Arg.Any<Func<Task>>())
            .Returns(async callInfo =>
            {
                var function = callInfo.Arg<Func<Task>>();
                await function();
            });

        openBankingService.SyncConnectionAsync(Arg.Any<Guid>(), SyncTypes.All, _cancellationTokenSource.Token)
            .Returns(Task.CompletedTask, Task.FromException(new InvalidOperationException("boom")), Task.CompletedTask);

        var subject = new SyncAllOpenBankingDetailsAsync(openBankingService, factory, syncService);

        await subject.ExecuteAsync(BuildTickerFunctionContext(), _cancellationTokenSource.Token);

        await openBankingService.Received(3)
            .SyncConnectionAsync(Arg.Any<Guid>(), SyncTypes.All, _cancellationTokenSource.Token);

        await syncService.Received(1).NotifySyncComplete();
    }

    [Test]
    public async Task ExecuteAsync_WhenUsersHaveNoConnections_DoesNotSyncAndStillNotifiesComplete()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"sync-job-empty-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        var openBankingService = Substitute.For<IOpenBankingService>();
        var syncService = Substitute.For<IBackgroundSyncService>();

        openBankingService.RunFunctionAsUser(Arg.Any<Guid>(), Arg.Any<Func<Task>>())
            .Returns(async callInfo =>
            {
                var function = callInfo.Arg<Func<Task>>();
                await function();
            });

        var subject = new SyncAllOpenBankingDetailsAsync(openBankingService, factory, syncService);

        await subject.ExecuteAsync(BuildTickerFunctionContext(), _cancellationTokenSource.Token);

        await openBankingService.DidNotReceive()
            .SyncConnectionAsync(Arg.Any<Guid>(), SyncTypes.All, _cancellationTokenSource.Token);

        await syncService.Received(1).NotifySyncComplete();
    }

    private static TickerFunctionContext BuildTickerFunctionContext()
    {
        var context = new TickerFunctionContext();

        if (context.CronOccurrenceOperations is null)
        {
            FieldInfo? backingField = typeof(TickerFunctionContext)
                .GetField("<CronOccurrenceOperations>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            backingField?.SetValue(context, new CronOccurrenceOperations());
        }

        PropertyInfo? skipActionProperty = typeof(CronOccurrenceOperations)
            .GetProperty("SkipIfAlreadyRunningAction", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        skipActionProperty?.SetValue(context.CronOccurrenceOperations, (Action)(() => { }));

        return context;
    }

    private static async Task SeedUsersWithProvidersAsync(
        IDbContextFactory<FinanceTrackerContext> contextFactory,
        CancellationToken cancellationToken,
        int primaryProviderCount,
        int secondaryProviderCount)
    {
        await SeedUsersAsync(contextFactory, cancellationToken);

        await using FinanceTrackerContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        context.BankConnections.AddRange(BuildConnections(PrimaryUserId, primaryProviderCount));
        context.BankConnections.AddRange(BuildConnections(SecondaryUserId, secondaryProviderCount));

        await context.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<BankConnection> BuildConnections(Guid userId, int count) =>
        Enumerable.Range(0, count).Select(i =>
            new BankConnection(userId, $"ob-{i}", $"Provider-{i}", $"access-{i}", ["accounts"], [1, 2, 3]));
}
