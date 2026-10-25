using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

public sealed class BankConnectionScenario : ScenarioBuilder
{
    public BankConnectionScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <param name="create">Builds the connection for the owning user id, generated when omitted.</param>
    public Task<TestingScenario> WithConnection(string connectionName, string username,
        Func<Guid, BankConnection>? create = null)
    {
        Guid userId = Scenario.UserMappings[username];
        BankConnection connection = create?.Invoke(userId) ?? new BankConnectionFaker(userId).Generate();

        Scenario.ConnectionMappings.Add(connectionName, connection.Id);
        return PersistAsync(context => context.BankConnections.Add(connection));
    }

    /// <summary>
    /// Issues a token for the connection, revoking any active token.
    /// </summary>
    /// <param name="issuedAt">Now when omitted.</param>
    public Task<TestingScenario> WithAccessToken(string connectionName, DateTime? issuedAt = null,
        int expiresInSeconds = 3600, string? token = null, string? refreshToken = null) =>
        PersistAsync(async context =>
        {
            BankConnection connection = await LoadAsync(context, connectionName);
            connection.IssueToken(token ?? $"token-{Guid.NewGuid()}", refreshToken ?? $"refresh-{Guid.NewGuid()}",
                issuedAt ?? DateTime.UtcNow, expiresInSeconds);
        });

    /// <summary>
    /// Backdates when consent was granted, it is otherwise stamped on construction.
    /// </summary>
    public Task<TestingScenario> WithConsentGrantedAt(string connectionName, DateTime consentGrantedAt) =>
        PersistAsync(async context =>
        {
            BankConnection connection = await LoadAsync(context, connectionName);
            context.Entry(connection).Property(x => x.ConsentGrantedAt).CurrentValue = consentGrantedAt;
        });

    /// <summary>
    /// Revokes the active token at <paramref name="revokedAt"/>.
    /// </summary>
    public Task<TestingScenario> WithRevokedToken(string connectionName, DateTime revokedAt) =>
        PersistAsync(async context => (await LoadAsync(context, connectionName)).RevokeActiveToken(revokedAt));

    private Task<BankConnection> LoadAsync(Infrastructure.FinanceTrackerContext context, string connectionName)
    {
        Guid connectionId = Scenario.ConnectionMappings[connectionName];
        return context.BankConnections
            .Include(x => x.AccessTokens)
            .SingleAsync(x => x.Id == connectionId, Scenario.CancellationToken);
    }
}
