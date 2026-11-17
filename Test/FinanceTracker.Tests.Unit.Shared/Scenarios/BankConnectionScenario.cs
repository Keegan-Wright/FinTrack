using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// Seeds bank connections and their tokens, mapped by name in <see cref="TestingScenario.ConnectionMappings"/>.
/// </summary>
public sealed class BankConnectionScenario : ScenarioBuilder
{
    /// <param name="scenario">The scenario it seeds.</param>
    public BankConnectionScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <summary>
    /// Adds a bank connection for the user.
    /// </summary>
    /// <param name="connectionName">The name to map the connection by.</param>
    /// <param name="username">The user who owns it, by their scenario name.</param>
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
    /// <param name="connectionName">The connection, by its scenario name.</param>
    /// <param name="issuedAt">Now when omitted.</param>
    /// <param name="expiresInSeconds">How long the access token lasts.</param>
    /// <param name="token">The access token, made up when omitted.</param>
    /// <param name="refreshToken">The refresh token, made up when omitted.</param>
    public Task<TestingScenario> WithAccessToken(string connectionName, DateTime? issuedAt = null,
        int expiresInSeconds = 3600, string? token = null, string? refreshToken = null) =>
        PersistAsync(async context =>
        {
            BankConnection connection = await LoadAsync(context, connectionName);
            connection.IssueToken(token ?? $"token-{Guid.NewGuid()}", refreshToken ?? $"refresh-{Guid.NewGuid()}",
                issuedAt ?? DateTime.UtcNow, expiresInSeconds);
        });

    /// <summary>
    /// Sets when consent ends, e.g. in the past for a lapsed connection. It is otherwise set on construction.
    /// </summary>
    /// <param name="connectionName">The connection, by its scenario name.</param>
    /// <param name="consentExpiresAt">When consent ends.</param>
    public Task<TestingScenario> WithConsentExpiresAt(string connectionName, DateTime consentExpiresAt) =>
        PersistAsync(async context =>
        {
            BankConnection connection = await LoadAsync(context, connectionName);
            context.Entry(connection).Property(x => x.ConsentExpiresAt).CurrentValue = consentExpiresAt;
        });

    /// <summary>
    /// Has the connection failing to sync since <paramref name="since"/>, as after a failed sync.
    /// </summary>
    /// <param name="connectionName">The connection, by its scenario name.</param>
    /// <param name="since">When it started failing.</param>
    public Task<TestingScenario> WithSyncFailing(string connectionName, DateTime since) =>
        PersistAsync(async context => (await LoadAsync(context, connectionName)).RecordSyncFailed(since));

    /// <summary>
    /// Revokes the active token at <paramref name="revokedAt"/>.
    /// </summary>
    /// <param name="connectionName">The connection, by its scenario name.</param>
    /// <param name="revokedAt">When it was revoked.</param>
    public Task<TestingScenario> WithRevokedToken(string connectionName, DateTime revokedAt) =>
        PersistAsync(async context => (await LoadAsync(context, connectionName)).RevokeActiveToken(revokedAt));

    /// <summary>
    /// Loads the connection to change, with its tokens.
    /// </summary>
    /// <param name="context">The context to load it in.</param>
    /// <param name="connectionName">The connection, by its scenario name.</param>
    private Task<BankConnection> LoadAsync(Infrastructure.FinanceTrackerContext context, string connectionName)
    {
        Guid connectionId = Scenario.ConnectionMappings[connectionName];
        return context.BankConnections
            .Include(x => x.AccessTokens)
            .SingleAsync(x => x.Id == connectionId, Scenario.CancellationToken);
    }
}
