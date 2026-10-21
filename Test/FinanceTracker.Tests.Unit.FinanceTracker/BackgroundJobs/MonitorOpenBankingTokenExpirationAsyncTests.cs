using FinanceTracker.BackgroundJobs;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.FinanceTracker.BackgroundJobs;

public class MonitorOpenBankingTokenExpirationAsyncTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    private static readonly TimeSpan ConsentLifetime = TimeSpan.FromDays(90);

    private readonly IBackgroundSyncService _syncService = Substitute.For<IBackgroundSyncService>();

    [Test]
    public async Task ExecuteAsync_ConsentLapsed_RevokesActiveToken()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank"))
            .Do(s => s.BankConnectionScenario.WithConsentGrantedAt("bank", Lapsed()));

        MonitorOpenBankingTokenExpirationAsync subject = BuildSubject();
        DateTime before = DateTime.UtcNow;

        // Act
        await subject.ExecuteAsync(TickerFunctionContextFactory.Create(), CancellationToken);

        // Assert
        BankConnection connection = await LoadConnectionAsync("bank");

        using (Assert.Multiple())
        {
            await Assert.That(connection.ActiveToken).IsNull();
            await Assert.That(connection.AccessTokens.Single().RevokedAt!.Value).IsGreaterThanOrEqualTo(before);
        }
    }

    [Test]
    public async Task ExecuteAsync_ConsentStillValid_KeepsActiveToken()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank"))
            .Do(s => s.BankConnectionScenario.WithConsentGrantedAt("bank",
                DateTime.UtcNow - ConsentLifetime + TimeSpan.FromHours(1)));

        MonitorOpenBankingTokenExpirationAsync subject = BuildSubject();

        // Act
        await subject.ExecuteAsync(TickerFunctionContextFactory.Create(), CancellationToken);

        // Assert
        BankConnection connection = await LoadConnectionAsync("bank");
        await Assert.That(connection.ActiveToken).IsNotNull();
    }

    [Test]
    public async Task ExecuteAsync_ConsentLapsedButAlreadyRevoked_LeavesRevocationUnchanged()
    {
        // Arrange
        DateTime revokedAt = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", revokedAt.AddDays(-1)))
            .Do(s => s.BankConnectionScenario.WithConsentGrantedAt("bank", Lapsed()));

        await RevokeActiveTokenAsync("bank", revokedAt);

        MonitorOpenBankingTokenExpirationAsync subject = BuildSubject();

        // Act
        await subject.ExecuteAsync(TickerFunctionContextFactory.Create(), CancellationToken);

        // Assert
        BankConnection connection = await LoadConnectionAsync("bank");
        await Assert.That(connection.AccessTokens.Single().RevokedAt).IsEqualTo(revokedAt);
    }

    [Test]
    public async Task ExecuteAsync_OnlyRevokesLapsedConnectionsAcrossUsers()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("lapsed", Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("lapsed"))
            .Do(s => s.BankConnectionScenario.WithConsentGrantedAt("lapsed", Lapsed()))
            .Do(s => s.BankConnectionScenario.WithConnection("valid", Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("valid"))
            .Do(s => s.BankConnectionScenario.WithConnection("their-lapsed", OtherUsername))
            .Do(s => s.BankConnectionScenario.WithAccessToken("their-lapsed"))
            .Do(s => s.BankConnectionScenario.WithConsentGrantedAt("their-lapsed", Lapsed()));

        MonitorOpenBankingTokenExpirationAsync subject = BuildSubject();

        // Act
        await subject.ExecuteAsync(TickerFunctionContextFactory.Create(), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((await LoadConnectionAsync("lapsed")).ActiveToken).IsNull();
            await Assert.That((await LoadConnectionAsync("their-lapsed")).ActiveToken).IsNull();
            await Assert.That((await LoadConnectionAsync("valid")).ActiveToken).IsNotNull();
        }
    }

    [Test]
    public async Task ExecuteAsync_ConsentLapsed_KeepsConnectionAndTokenHistory()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", DateTime.UtcNow.AddDays(-100)))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank"))
            .Do(s => s.BankConnectionScenario.WithConsentGrantedAt("bank", Lapsed()));

        MonitorOpenBankingTokenExpirationAsync subject = BuildSubject();

        // Act
        await subject.ExecuteAsync(TickerFunctionContextFactory.Create(), CancellationToken);

        // Assert
        BankConnection connection = await LoadConnectionAsync("bank");

        using (Assert.Multiple())
        {
            await Assert.That(connection.AccessTokens.Count).IsEqualTo(2);
            await Assert.That(connection.AccessTokens.All(x => x.IsRevoked)).IsTrue();
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ExecuteAsync_NotifiesCompleteOnce(bool hasLapsedConnection)
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank"))
            .Do(s => hasLapsedConnection
                ? s.BankConnectionScenario.WithConsentGrantedAt("bank", Lapsed())
                : Task.FromResult(s));

        MonitorOpenBankingTokenExpirationAsync subject = BuildSubject();

        // Act
        await subject.ExecuteAsync(TickerFunctionContextFactory.Create(), CancellationToken);

        // Assert
        await _syncService.Received(1).NotifySyncComplete();
    }

    [Test]
    public async Task ExecuteAsync_NoConnections_StillNotifiesComplete()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        MonitorOpenBankingTokenExpirationAsync subject = BuildSubject();

        // Act
        await subject.ExecuteAsync(TickerFunctionContextFactory.Create(), CancellationToken);

        // Assert
        await _syncService.Received(1).NotifySyncComplete();
    }

    private static DateTime Lapsed() => DateTime.UtcNow - ConsentLifetime - TimeSpan.FromHours(1);

    private async Task RevokeActiveTokenAsync(string connectionName, DateTime revokedAt)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid id = Scenario.ConnectionMappings[connectionName];
        BankConnection connection = await context.BankConnections.Include(x => x.AccessTokens)
            .SingleAsync(x => x.Id == id, CancellationToken);
        connection.RevokeActiveToken(revokedAt);
        await context.SaveChangesAsync(CancellationToken);
    }

    private async Task<BankConnection> LoadConnectionAsync(string connectionName)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid id = Scenario.ConnectionMappings[connectionName];
        return await context.BankConnections.Include(x => x.AccessTokens)
            .SingleAsync(x => x.Id == id, CancellationToken);
    }

    private MonitorOpenBankingTokenExpirationAsync BuildSubject() => new(DbContextFactory, _syncService);
}
