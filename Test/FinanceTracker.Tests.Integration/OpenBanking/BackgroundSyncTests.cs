using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;
using FinanceTracker.Tests.Unit.Shared.Fakers.TrueLayer;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Enums;

namespace FinanceTracker.Tests.Integration.OpenBanking;

/// <summary>
/// Runs the real background jobs through TickerQ against <see cref="FakeTrueLayer"/>. The scheduled jobs visit every
/// connection in the database, so these tests don't run alongside each other.
/// </summary>
[NotInParallel(nameof(BackgroundSyncTests))]
public class BackgroundSyncTests : IntegrationTestBase
{
    private const string SyncFunction = "SyncAllOpenBankingDetailsAsync";
    private const string UserSyncFunction = "SyncUserOpenBankingDetailsAsync";
    private const string MonitorFunction = "MonitorOpenBankingTokenExpirationAsync";
    private static readonly TimeSpan JobTimeout = TimeSpan.FromSeconds(90);

    [Test]
    public async Task Sync_ImportsAccountsAndTheirData()
    {
        // Arrange
        string username = UniqueName();
        string accessToken = UniqueName("token");
        await Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", token: accessToken));

        ExternalOpenBankingAccount account = new ExternalOpenBankingAccountFaker().Generate();
        ExternalOpenBankingAccountBalance balance = new ExternalOpenBankingAccountBalanceFaker().Generate();
        List<ExternalOpenBankingAccountTransaction> transactions = new ExternalOpenBankingAccountTransactionFaker().Generate(3);
        ExternalOpenBankingAccountTransaction pending = new ExternalOpenBankingAccountTransactionFaker().Generate();
        ExternalOpenBankingAccountStandingOrder standingOrder = new ExternalOpenBankingAccountStandingOrderFaker().Generate();
        ExternalOpenBankingDirectDebit directDebit = new ExternalOpenBankingDirectDebitFaker().Generate();

        await App.TrueLayer.StubAccountAsync(accessToken, account, [balance], transactions, [pending],
            [standingOrder], [directDebit]);

        // Act
        await RunSyncAsync();

        // Assert
        Guid connectionId = Scenario.ConnectionMappings["bank"];
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        BankAccount synced = await context.BankAccounts
            .Include(x => x.StandingOrders)
            .Include(x => x.DirectDebits)
            .SingleAsync(x => x.ConnectionId == connectionId, CancellationToken);

        List<BankTransaction> syncedTransactions = await context.BankTransactions
            .Where(x => x.AccountId == synced.Id)
            .ToListAsync(CancellationToken);

        List<SyncType> syncLog = await context.SyncLog
            .Where(x => x.AccountId == synced.Id)
            .Select(x => x.Type)
            .ToListAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(synced.ExternalId).IsEqualTo(account.AccountId);
            await Assert.That(synced.DisplayName).IsEqualTo(account.DisplayName);
            await Assert.That(synced.Balance!.Current).IsEqualTo(balance.Current);
            await Assert.That(synced.Balance!.Available).IsEqualTo(balance.Available);
            await Assert.That(synced.StandingOrders).Count().IsEqualTo(1);
            await Assert.That(synced.DirectDebits).Count().IsEqualTo(1);
            await Assert.That(syncedTransactions.Select(x => x.ExternalId))
                .IsEquivalentTo(transactions.Append(pending).Select(x => x.TransactionId));
            await Assert.That(syncLog).IsEquivalentTo(
                [SyncType.Account, SyncType.Balance, SyncType.StandingOrders, SyncType.DirectDebits, SyncType.Transactions]);
        }
    }

    [Test]
    public async Task Sync_WithAnExpiredToken_RefreshesItFirst()
    {
        // Arrange
        string username = UniqueName();
        string expiredToken = UniqueName("expired");
        string refreshToken = UniqueName("refresh");
        await Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", DateTime.UtcNow.AddHours(-2),
                expiresInSeconds: 3600, token: expiredToken, refreshToken: refreshToken));

        ExternalOpenBankingAccessResponse refreshed = new ExternalOpenBankingAccessResponseFaker().Generate();
        await App.TrueLayer.StubRefreshAsync(refreshToken, refreshed);

        ExternalOpenBankingAccount account = new ExternalOpenBankingAccountFaker().Generate();
        await App.TrueLayer.StubAccountAsync(refreshed.AccessToken, account);

        // Act
        await RunSyncAsync();

        // Assert
        Guid connectionId = Scenario.ConnectionMappings["bank"];
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        BankConnection connection = await context.BankConnections
            .Include(x => x.AccessTokens)
            .SingleAsync(x => x.Id == connectionId, CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(App.TrueLayer.RefreshRequestsFor(refreshToken)).Count().IsEqualTo(1);
            await Assert.That(App.TrueLayer.RequestsWithToken(expiredToken)).IsEmpty();
            await Assert.That(App.TrueLayer.RequestsWithToken(refreshed.AccessToken)).IsNotEmpty();
            await Assert.That(connection.ActiveToken!.Token).IsEqualTo(refreshed.AccessToken);
            await Assert.That(connection.ActiveToken!.RefreshToken).IsEqualTo(refreshed.RefreshToken);
            await Assert.That(await context.BankAccounts.AnyAsync(x => x.ConnectionId == connectionId, CancellationToken)).IsTrue();
        }
    }

    [Test]
    public async Task UserSync_OnlySyncsTheRequestingUsersConnections()
    {
        // Arrange
        string username = UniqueName();
        string otherUsername = UniqueName();
        string accessToken = UniqueName("token");
        string otherAccessToken = UniqueName("token");
        await Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.UserScenario.WithUser(otherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("mine", username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("mine", token: accessToken))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", otherUsername))
            .Do(s => s.BankConnectionScenario.WithAccessToken("theirs", token: otherAccessToken));

        await App.TrueLayer.StubAccountAsync(accessToken, new ExternalOpenBankingAccountFaker().Generate());
        await App.TrueLayer.StubAccountAsync(otherAccessToken, new ExternalOpenBankingAccountFaker().Generate());

        // Matches the request the resync button queues, SyncUserOpenBankingDetailsRequest
        byte[] request = TickerHelper.CreateTickerRequest(new { UserId = Scenario.UserMappings[username] });

        // Act
        await RunJobAsync(UserSyncFunction, request);

        // Assert
        Guid connectionId = Scenario.ConnectionMappings["mine"];
        Guid otherConnectionId = Scenario.ConnectionMappings["theirs"];
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(await context.BankAccounts.AnyAsync(x => x.ConnectionId == connectionId, CancellationToken)).IsTrue();
            await Assert.That(await context.BankAccounts.AnyAsync(x => x.ConnectionId == otherConnectionId, CancellationToken)).IsFalse();
            await Assert.That(App.TrueLayer.RequestsWithToken(otherAccessToken)).IsEmpty();
        }
    }

    [Test]
    public async Task ConsentMonitor_RevokesLapsedConsent()
    {
        // Arrange
        string username = UniqueName();
        await Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank"))
            .Do(s => s.BankConnectionScenario.WithConsentGrantedAt("bank", DateTime.UtcNow.AddDays(-91)));

        // Act
        await RunJobAsync(MonitorFunction);

        // Assert
        Guid connectionId = Scenario.ConnectionMappings["bank"];
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        BankConnection connection = await context.BankConnections
            .Include(x => x.AccessTokens)
            .SingleAsync(x => x.Id == connectionId, CancellationToken);

        await Assert.That(connection.ActiveToken).IsNull();
    }

    private Task RunSyncAsync() => RunJobAsync(SyncFunction);

    /// <summary>
    /// Queues <paramref name="function"/> the same way the app does and waits for TickerQ to finish it.
    /// </summary>
    private async Task RunJobAsync(string function, byte[]? request = null)
    {
        Guid tickerId = Guid.NewGuid();
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            context.TimeTickerEntities.Add(new TimeTickerEntity
            {
                Id = tickerId,
                Function = function,
                Request = request,
                ExecutionTime = DateTime.UtcNow
            });
            await context.SaveChangesAsync(CancellationToken);
        }

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        timeout.CancelAfter(JobTimeout);

        while (true)
        {
            await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(timeout.Token);
            TimeTickerEntity ticker = await context.TimeTickerEntities.AsNoTracking()
                .SingleAsync(x => x.Id == tickerId, timeout.Token);

            switch (ticker.Status)
            {
                case TickerStatus.Done or TickerStatus.DueDone:
                    return;
                case TickerStatus.Failed or TickerStatus.Cancelled or TickerStatus.Skipped:
                    throw new InvalidOperationException(
                        $"{function} finished as {ticker.Status}: {ticker.ExceptionMessage}");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), timeout.Token);
        }
    }
}
