using FinanceTracker.BackgroundJobs;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.FinanceTracker.BackgroundJobs;

public class MonitorOpenBankingTokenExpirationAsyncTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    [Test]
    public async Task ExecuteAsync_ConsentLapsed_RevokesActiveToken()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank"))
            .Do(s => s.BankConnectionScenario.WithConsentExpiresAt("bank", Lapsed()));

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
            .Do(s => s.BankConnectionScenario.WithConsentExpiresAt("bank", DateTime.UtcNow.AddHours(1)));

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
            .Do(s => s.BankConnectionScenario.WithConsentExpiresAt("bank", Lapsed()))
            .Do(s => s.BankConnectionScenario.WithRevokedToken("bank", revokedAt));

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
            .Do(s => s.BankConnectionScenario.WithConsentExpiresAt("lapsed", Lapsed()))
            .Do(s => s.BankConnectionScenario.WithConnection("valid", Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("valid"))
            .Do(s => s.BankConnectionScenario.WithConnection("their-lapsed", OtherUsername))
            .Do(s => s.BankConnectionScenario.WithAccessToken("their-lapsed"))
            .Do(s => s.BankConnectionScenario.WithConsentExpiresAt("their-lapsed", Lapsed()));

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
            .Do(s => s.BankConnectionScenario.WithConsentExpiresAt("bank", Lapsed()));

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

    private static DateTime Lapsed() => DateTime.UtcNow.AddHours(-1);

    private async Task<BankConnection> LoadConnectionAsync(string connectionName)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid id = Scenario.ConnectionMappings[connectionName];
        return await context.BankConnections.Include(x => x.AccessTokens)
            .SingleAsync(x => x.Id == id, CancellationToken);
    }

    private MonitorOpenBankingTokenExpirationAsync BuildSubject() => new(DbContextFactory, TimeProvider.System);
}
