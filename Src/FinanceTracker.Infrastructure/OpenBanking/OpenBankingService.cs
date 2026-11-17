using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.OpenBanking;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.OpenBanking;

/// <inheritdoc cref="IOpenBankingService"/>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IOpenBankingService>]
public class OpenBankingService : ServiceBase<OpenBankingService>, IOpenBankingService
{
    /// <summary>
    /// How long consent lasts.
    /// </summary>
    private static readonly TimeSpan DefaultConsentLifetime = TimeSpan.FromDays(90);

    /// <summary>
    /// The cache key for the banks the provider offers.
    /// </summary>
    private const string ProvidersCacheKey = "open-banking:providers";

    // The same for every user and rarely changes, so the provider is asked at most every few hours
    private static readonly HybridCacheEntryOptions ProvidersCacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(6),
        LocalCacheExpiration = TimeSpan.FromHours(6)
    };

    private readonly TimeProvider _timeProvider;
    private readonly IOpenBankingApiService _openBankingApiService;
    private readonly HybridCache _cache;
    private readonly IOpenBankingSyncScheduler _openBankingSyncScheduler;

    /// <param name="currentUser">The signed in user, or who the work runs as.</param>
    /// <param name="financeTrackerContextFactory">Makes the contexts it reads and saves through.</param>
    /// <param name="openBankingApiService">Calls the bank's provider.</param>
    /// <param name="logger">Its logger.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="cache">Caches the banks the provider offers.</param>
    /// <param name="openBankingSyncScheduler">Queues syncs.</param>
    public OpenBankingService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        IOpenBankingApiService openBankingApiService, ILogger<OpenBankingService> logger,
        TimeProvider timeProvider, HybridCache cache, IOpenBankingSyncScheduler openBankingSyncScheduler)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _openBankingApiService = openBankingApiService;
        _timeProvider = timeProvider;
        _cache = cache;
        _openBankingSyncScheduler = openBankingSyncScheduler;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<OpenBankingProvider> GetOpenBankingProvidersForClientAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        List<OpenBankingProvider> providers = await _cache.GetOrCreateAsync(ProvidersCacheKey, LoadProvidersAsync,
            ProvidersCacheOptions, cancellationToken: cancellationToken);

        foreach (OpenBankingProvider provider in providers)
        {
            yield return provider;
        }
    }

    // A failed request throws, so nothing is cached and the next visit asks again
    /// <summary>
    /// Loads the banks the provider offers, by name, with their logos.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    private async ValueTask<List<OpenBankingProvider>> LoadProvidersAsync(CancellationToken cancellationToken)
    {
        List<OpenBankingProvider> providers = [];
        await foreach (ExternalOpenBankingProvider provider in _openBankingApiService
                           .GetAvailableProvidersAsync(cancellationToken)
                           .OrderBy(static x => x.DisplayName).WithCancellation(cancellationToken))
        {
            providers.Add(await provider.ToContractAsync(cancellationToken));
        }

        return providers;
    }

    /// <inheritdoc/>
    public Task<string> BuildAuthUrlAsync(ProviderSetupUrl setupProvider, CancellationToken cancellationToken) =>
        Task.FromResult(_openBankingApiService.BuildAuthUrl(setupProvider.ProviderIds, setupProvider.Scopes));

    /// <inheritdoc/>
    public async Task<bool> AddVendorViaAccessCodeAsync(AddVendor addVendor, CancellationToken cancellationToken)
    {
        ExternalOpenBankingAccessResponse providerAccessToken =
            await _openBankingApiService.ExchangeCodeForAccessTokenAsync(addVendor.AccessCode, cancellationToken);
        ExternalOpenBankingAccountConnectionResponse providerInformation =
            await _openBankingApiService.GetProviderInformation(providerAccessToken.AccessToken, cancellationToken);

        // What happened to each, logged once saved
        List<(Guid Id, string Action)> connected = [];
        await using (FinanceTrackerContext context =
                     await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken))
        {
            await foreach (ExternalOpenBankingAccountConnection externalConnection in
                           (providerInformation.Results ?? throw new InvalidOperationException())
                           .WithCancellation(cancellationToken))
            {
                ExternalOpenBankingAccountConnectionProvider provider =
                    externalConnection.Provider ?? throw new InvalidOperationException();
                List<string> scopes = externalConnection.Scopes is null
                    ? []
                    : await externalConnection.Scopes.ToListAsync(cancellationToken);

                BankConnection? connection = await context.BankConnections.OwnedBy(UserId)
                    .WhereEncryptedEquals(x => x.ExternalProviderId, provider.ProviderId, context)
                    .Include(x => x.AccessTokens.Where(t => t.RevokedAt == null))
                    .SingleOrDefaultAsync(cancellationToken);

                DateTime consentExpiresAt = ConsentExpiresAt(externalConnection);
                if (connection is null)
                {
                    connection = new BankConnection(UserId, provider.ProviderId, provider.DisplayName,
                        addVendor.AccessCode, scopes,
                        await _openBankingApiService.GetProviderLogoAsync(provider.LogoUri, cancellationToken),
                        _timeProvider.UtcNow, consentExpiresAt);
                    context.BankConnections.Add(connection);
                    connected.Add((connection.Id, "connected"));
                }
                else
                {
                    // Adding a provider that is already connected re-authenticates it, as does renewing it
                    connection.Reauthenticate(addVendor.AccessCode, scopes, _timeProvider.UtcNow, consentExpiresAt);
                    connected.Add((connection.Id, "reconnected"));
                }

                connection.IssueToken(providerAccessToken.AccessToken, providerAccessToken.RefreshToken,
                    _timeProvider.UtcNow, providerAccessToken.ExpiresIn);
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        foreach ((Guid id, string action) in connected)
        {
            Logger.LogChanged(UserId, action, "bank connection", id);
        }

        // Synced by the background job like any other sync, so transfers are matched and the user is told when it's
        // done
        await _openBankingSyncScheduler.ScheduleUserSyncAsync(UserId, cancellationToken);

        return true;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<BankConnectionSummary> GetConnectionsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Names are encrypted, so they're sorted in memory
        List<BankConnection> connections = await context.BankConnections.OwnedBy(UserId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        Dictionary<Guid, DateTime> recurringPaymentsUpdatedAt =
            await RecurringPaymentsUpdatedAtAsync(context, connections, cancellationToken);

        DateTime now = _timeProvider.UtcNow;
        foreach (BankConnection connection in connections.OrderBy(static x => x.Name))
        {
            yield return new BankConnectionSummary
            {
                ConnectionId = connection.Id,
                Name = connection.Name,
                Logo = connection.Logo,
                ConsentExpiresAt = connection.ConsentExpiresAt,
                ConsentState = ConsentStateOf(connection, now),
                RecurringPaymentsUpdatedAt = UpdatedAtOrNull(recurringPaymentsUpdatedAt, connection.Id)
            };
        }
    }

    // A connection missing from the dictionary has never had them, which is null rather than DateTime's default
    /// <summary>
    /// When a connection's standing orders and direct debits last updated, null when never.
    /// </summary>
    /// <param name="updatedAt">When each connection's last updated.</param>
    /// <param name="connectionId">The connection.</param>
    private static DateTime? UpdatedAtOrNull(Dictionary<Guid, DateTime> updatedAt, Guid connectionId)
    {
        if (updatedAt.TryGetValue(connectionId, out DateTime at))
        {
            return at;
        }

        return null;
    }

    /// <summary>
    /// When each of <paramref name="connections"/> last had its standing orders or direct debits fetched, from the
    /// entries a sync leaves only when it saved them. Connections that never have are left out.
    /// </summary>
    /// <param name="context">The context to read through.</param>
    /// <param name="connections">The connections.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private static async Task<Dictionary<Guid, DateTime>> RecurringPaymentsUpdatedAtAsync(
        FinanceTrackerContext context, List<BankConnection> connections, CancellationToken cancellationToken)
    {
        List<Guid> connectionIds = connections.Select(static x => x.Id).ToList();

        return await context.SyncLog
            .Where(static x => x.Type == SyncType.StandingOrders || x.Type == SyncType.DirectDebits)
            .Join(context.BankAccounts, static x => x.AccountId, static x => x.Id,
                static (entry, account) => new { account.ConnectionId, entry.OccurredAt })
            .Where(x => connectionIds.Contains(x.ConnectionId))
            .GroupBy(static x => x.ConnectionId)
            .Select(static x => new { ConnectionId = x.Key, UpdatedAt = x.Max(e => e.OccurredAt) })
            .ToDictionaryAsync(static x => x.ConnectionId, static x => x.UpdatedAt, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<string> GetRenewalUrlAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Only the latest token, revoked or not, renewing works for a while after consent has lapsed
        BankConnection connection = await context.BankConnections.OwnedBy(UserId)
            .AsNoTracking()
            .Include(x => x.AccessTokens.OrderByDescending(t => t.IssuedAt).Take(1))
            .SingleOrNotFoundAsync(x => x.Id == connectionId, "connection", cancellationToken);

        return await _openBankingApiService.GetReauthenticationUrlAsync(connection.RefreshTokenForRenewal(),
            cancellationToken);
    }

    /// <summary>
    /// Whether a connection's consent has ended, ends soon or is fine.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="now">The time now, in UTC.</param>
    private static ConsentState ConsentStateOf(BankConnection connection, DateTime now)
    {
        if (connection.HasConsentLapsed(now))
        {
            return ConsentState.Lapsed;
        }

        if (connection.NeedsRenewal(now))
        {
            return ConsentState.RenewSoon;
        }

        return ConsentState.Active;
    }

    /// <summary>
    /// When the provider says the consent ends, or the usual lifetime from now if it doesn't say.
    /// </summary>
    /// <param name="externalConnection">The connection as TrueLayer sends it.</param>
    private DateTime ConsentExpiresAt(ExternalOpenBankingAccountConnection externalConnection)
    {
        DateTime reported = externalConnection.ConsentExpiresAt.ToUtc();
        if (reported > _timeProvider.UtcNow)
        {
            return reported;
        }

        return _timeProvider.UtcNow + DefaultConsentLifetime;
    }
}
