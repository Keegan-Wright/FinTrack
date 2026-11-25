using System.Diagnostics;
using FinanceTracker.Contracts;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Common;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;
using FinanceTracker.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.OpenBanking;

/// <inheritdoc cref="IOpenBankingSyncService"/>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IOpenBankingSyncService>]
public sealed class OpenBankingSyncService : ServiceBase<OpenBankingSyncService>, IOpenBankingSyncService
{
    private static readonly TimeSpan TransactionSyncOverlap = TimeSpan.FromDays(7);

    /// <summary>
    /// The consent scope that lets a connection read its cards. Connections without it aren't asked for cards.
    /// </summary>
    private const string CardsScope = "cards";

    private readonly TimeProvider _timeProvider;
    private readonly IOpenBankingApiService _openBankingApiService;
    private readonly IAccountSnapshotFetcher _accountSnapshotFetcher;
    private readonly IAccountSnapshotWriter _accountSnapshotWriter;
    private readonly FinTrackMetrics _metrics;

    /// <param name="currentUser">The signed in user, or who the work runs as.</param>
    /// <param name="financeTrackerContextFactory">Makes the contexts it reads and saves through.</param>
    /// <param name="openBankingApiService">Calls the bank's provider.</param>
    /// <param name="logger">Its logger.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="accountSnapshotFetcher">Fetches an account from the bank.</param>
    /// <param name="accountSnapshotWriter">Saves an account's snapshot.</param>
    /// <param name="metrics">Records its metrics.</param>
    public OpenBankingSyncService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        IOpenBankingApiService openBankingApiService, ILogger<OpenBankingSyncService> logger,
        TimeProvider timeProvider, IAccountSnapshotFetcher accountSnapshotFetcher,
        IAccountSnapshotWriter accountSnapshotWriter, FinTrackMetrics metrics)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _openBankingApiService = openBankingApiService;
        _timeProvider = timeProvider;
        _accountSnapshotFetcher = accountSnapshotFetcher;
        _accountSnapshotWriter = accountSnapshotWriter;
        _metrics = metrics;
    }

    /// <inheritdoc/>
    public async Task PerformSyncAsync(SyncTypes syncFlags, CancellationToken cancellationToken)
    {
        List<Guid> connectionIds;
        await using (FinanceTrackerContext context =
                     await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken))
        {
            connectionIds = await context.BankConnections.OwnedBy(UserId)
                .Select(static x => x.Id)
                .ToListAsync(cancellationToken);
        }

        foreach (Guid connectionId in connectionIds)
        {
            await SyncConnectionAsync(connectionId, syncFlags, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task SyncConnectionAsync(Guid connectionId, SyncTypes syncFlags, CancellationToken cancellationToken)
    {
        long startedAt = Stopwatch.GetTimestamp();
        ConnectionSyncTally tally = new();

        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        BankConnection connection = await context.BankConnections.OwnedBy(UserId)
            .Include(x => x.AccessTokens.Where(t => t.RevokedAt == null))
            .SingleOrNotFoundAsync(x => x.Id == connectionId, "connection", cancellationToken);

        string? authToken = await GetValidAccessTokenAsync(context, connection, cancellationToken);
        if (authToken is null)
        {
            Logger.LogConnectionNotAuthenticated(connection.Id, UserId);
            return;
        }

        ExternalOpenBankingListAllAccountsResponse accountsResponse =
            await _openBankingApiService.GetAllAccountsAsync(authToken, cancellationToken);

        List<ExternalOpenBankingAccount> externalAccounts = accountsResponse.Results is null
            ? []
            : await accountsResponse.Results.ToListAsync(cancellationToken);

        Dictionary<string, BankAccount> accounts = await context.BankAccounts
            .Where(x => x.ConnectionId == connection.Id)
            .Include(x => x.StandingOrders)
            .Include(x => x.DirectDebits)
            .AsSplitQuery()
            .ToDictionaryAsync(static x => x.ExternalId, cancellationToken);

        List<Guid> accountIds = accounts.Values.Select(static x => x.Id).ToList();
        Dictionary<Guid, DateTime> lastTransactionSync = await context.SyncLog
            .Where(x => accountIds.Contains(x.AccountId) && x.Type == SyncType.Transactions)
            .GroupBy(static x => x.AccountId)
            .Select(static x => new { AccountId = x.Key, OccurredAt = x.Max(s => s.OccurredAt) })
            .ToDictionaryAsync(static x => x.AccountId, static x => x.OccurredAt, cancellationToken);

        DateTime now = _timeProvider.UtcNow;
        SyncTypes enhancedAuthenticationWindowFlags = EnhancedAuthenticationWindowSyncFlags(connection, syncFlags, now);
        foreach (ExternalOpenBankingAccount externalAccount in externalAccounts)
        {
            if (accounts.TryGetValue(externalAccount.AccountId, out BankAccount? account))
            {
                account.UpdateDetails(externalAccount.AccountType, externalAccount.DisplayName,
                    externalAccount.Currency);
            }
            else
            {
                account = new BankAccount(connection.UserId, connection.Id, externalAccount.AccountId,
                    externalAccount.AccountType, externalAccount.DisplayName, externalAccount.Currency);
                context.BankAccounts.Add(account);
                accounts.Add(externalAccount.AccountId, account);
            }

            // A span for each account, its fetches from the bank inside it
            using Activity? accountActivity = StartAccountActivity("Sync account", account);
            tally.Accounts++;

            account.UpdateIdentifier(AccountIdentifier.Create(externalAccount.AccountNumber?.SortCode,
                externalAccount.AccountNumber?.Number, externalAccount.AccountNumber?.Iban));
            context.SyncLog.Add(account.RecordSync(SyncType.Account, now));

            AccountSnapshot snapshot = await _accountSnapshotFetcher.FetchAccountAsync(connection, account, authToken,
                syncFlags, enhancedAuthenticationWindowFlags, TransactionsFrom(lastTransactionSync, account), tally,
                cancellationToken);

            await _accountSnapshotWriter.ApplyAsync(context, account, snapshot, now, tally, cancellationToken);
        }

        if (connection.Scopes.Contains(CardsScope))
        {
            await SyncCardsAsync(context, connection, accounts, lastTransactionSync, authToken, syncFlags, now, tally,
                cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        Summarise(connection, tally, Stopwatch.GetElapsedTime(startedAt));
    }

    /// <summary>
    /// Where the account's booked transactions are fetched from, a week before its last sync of them so late arrivals
    /// are caught. Null, for all of them, when it's never had them.
    /// </summary>
    /// <param name="lastTransactionSync">When each account's transactions last synced.</param>
    /// <param name="account">The account.</param>
    private static DateTime? TransactionsFrom(Dictionary<Guid, DateTime> lastTransactionSync, BankAccount account)
    {
        if (lastTransactionSync.TryGetValue(account.Id, out DateTime lastSync))
        {
            return lastSync - TransactionSyncOverlap;
        }

        return null;
    }

    /// <summary>
    /// What to fetch of what only the bank's enhanced authentication window releases, standing orders and direct
    /// debits. Banks refuse them a few minutes after the user authenticated, so outside that a sync leaves them out
    /// rather than have every account's fetches refused. The ones already stored are kept, and the next connect or
    /// renewal refreshes them.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="syncFlags">What the sync fetches.</param>
    /// <param name="now">The time now, in UTC.</param>
    private SyncTypes EnhancedAuthenticationWindowSyncFlags(BankConnection connection, SyncTypes syncFlags,
        DateTime now)
    {
        if (connection.IsWithinEnhancedAuthenticationWindow(now))
        {
            return syncFlags;
        }

        bool wanted = syncFlags.Includes(SyncTypes.StandingOrders) || syncFlags.Includes(SyncTypes.DirectDebits);
        if (wanted)
        {
            Logger.LogStandingOrdersAndDirectDebitsSkipped(connection.Id, UserId, connection.ConsentGrantedAt);
        }

        return default;
    }

    /// <summary>
    /// Logs what the connection's sync did and tags its span with the same, the job's <c>Sync connection</c>.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="tally">What the sync did.</param>
    /// <param name="took">How long it took.</param>
    private void Summarise(BankConnection connection, ConnectionSyncTally tally, TimeSpan took)
    {
        Logger.LogSyncedConnection(connection.Id, UserId, tally.Accounts, tally.Cards, tally.TransactionsAdded,
            tally.TransactionsUpdated, tally.PendingRemoved, tally.FailedFetches, (long)took.TotalMilliseconds);
        _metrics.TransactionsSynced(tally.TransactionsAdded, tally.TransactionsUpdated);
        _metrics.FetchesFailed(tally.FailedFetches);

        Activity? connectionActivity = Activity.Current;
        connectionActivity?.SetTag(TelemetryTags.Accounts, tally.Accounts);
        connectionActivity?.SetTag(TelemetryTags.Cards, tally.Cards);
        connectionActivity?.SetTag(TelemetryTags.TransactionsAdded, tally.TransactionsAdded);
        connectionActivity?.SetTag(TelemetryTags.TransactionsUpdated, tally.TransactionsUpdated);
        connectionActivity?.SetTag(TelemetryTags.PendingRemoved, tally.PendingRemoved);
        connectionActivity?.SetTag(TelemetryTags.FetchesFailed, tally.FailedFetches);
    }

    // A span for one account or card's sync
    /// <summary>
    /// Starts a span for work on one account, tagged with the account.
    /// </summary>
    /// <param name="name">The span's name.</param>
    /// <param name="account">The account.</param>
    private static Activity? StartAccountActivity(string name, BankAccount account)
    {
        Activity? activity = FinTrackActivitySources.Infrastructure.StartActivity(name);
        activity?.SetTag(TelemetryTags.AccountId, account.Id.ToString());
        return activity;
    }

    /// <summary>
    /// Syncs the connection's credit and charge cards like its accounts, each stored as a card
    /// <see cref="BankAccount"/>. Cards have no standing orders or direct debits.
    /// </summary>
    /// <param name="context">The context to save through.</param>
    /// <param name="connection">The connection.</param>
    /// <param name="accounts">The connection's accounts and cards so far.</param>
    /// <param name="lastTransactionSync">When each account's transactions last synced.</param>
    /// <param name="authToken">The connection's access token.</param>
    /// <param name="syncFlags">What to fetch.</param>
    /// <param name="now">The time now, in UTC.</param>
    /// <param name="tally">Counts what the sync does.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task SyncCardsAsync(FinanceTrackerContext context, BankConnection connection,
        Dictionary<string, BankAccount> accounts, Dictionary<Guid, DateTime> lastTransactionSync, string authToken,
        SyncTypes syncFlags, DateTime now, ConnectionSyncTally tally, CancellationToken cancellationToken)
    {
        ExternalOpenBankingListAllCardsResponse cardsResponse =
            await _openBankingApiService.GetAllCardsAsync(authToken, cancellationToken);

        List<ExternalOpenBankingCard> externalCards = cardsResponse.Results is null
            ? []
            : await cardsResponse.Results.ToListAsync(cancellationToken);

        foreach (ExternalOpenBankingCard externalCard in externalCards)
        {
            if (accounts.TryGetValue(externalCard.AccountId, out BankAccount? card))
            {
                card.UpdateDetails(externalCard.AccountType(), externalCard.DisplayName, externalCard.Currency);
            }
            else
            {
                card = new BankAccount(connection.UserId, connection.Id, externalCard.AccountId,
                    externalCard.AccountType(), externalCard.DisplayName, externalCard.Currency,
                    BankAccountKind.Card);
                context.BankAccounts.Add(card);
                accounts.Add(externalCard.AccountId, card);
            }

            using Activity? cardActivity = StartAccountActivity("Sync card", card);
            tally.Cards++;

            card.UpdateCard(externalCard.ToDetails());
            context.SyncLog.Add(card.RecordSync(SyncType.Account, now));

            AccountSnapshot snapshot = await _accountSnapshotFetcher.FetchCardAsync(connection, card, authToken,
                syncFlags, TransactionsFrom(lastTransactionSync, card), tally, cancellationToken);

            await _accountSnapshotWriter.ApplyAsync(context, card, snapshot, now, tally, cancellationToken);
        }
    }

    /// <summary>
    /// Returns a usable access token, refreshing and persisting a new one when the current token has lapsed.
    /// Null when the connection has no active consent.
    /// </summary>
    /// <param name="context">The context to save through.</param>
    /// <param name="connection">The connection.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<string?> GetValidAccessTokenAsync(FinanceTrackerContext context, BankConnection connection,
        CancellationToken cancellationToken)
    {
        AccessToken? activeToken = connection.ActiveToken;
        if (activeToken is null)
        {
            return null;
        }

        if (!activeToken.IsExpired(_timeProvider.UtcNow))
        {
            return activeToken.Token;
        }

        using Activity? refreshActivity = FinTrackActivitySources.Infrastructure.StartActivity("Refresh access token");
        ExternalOpenBankingAccessResponse refreshed =
            await _openBankingApiService.GetAccessTokenByRefreshTokenAsync(activeToken.RefreshToken,
                cancellationToken);

        AccessToken issued = connection.IssueToken(refreshed.AccessToken, refreshed.RefreshToken, _timeProvider.UtcNow,
            refreshed.ExpiresIn);

        // Persist straight away, the previous refresh token is no longer valid with the provider
        await context.SaveChangesAsync(cancellationToken);

        return issued.Token;
    }
}
