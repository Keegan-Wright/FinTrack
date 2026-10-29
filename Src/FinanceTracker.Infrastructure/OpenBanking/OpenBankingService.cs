using System.Runtime.CompilerServices;
using FinanceTracker.Contracts;
using FinanceTracker.Contracts.OpenBanking;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.OpenBanking;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IOpenBankingService>]
[Scoped<IOpenBankingSyncService>]
public class OpenBankingService : ServiceBase<OpenBankingService>, IOpenBankingService, IOpenBankingSyncService
{

    private static readonly TimeSpan TransactionSyncOverlap = TimeSpan.FromDays(7);

    /// <summary>
    /// How long consent lasts.
    /// </summary>
    private static readonly TimeSpan DefaultConsentLifetime = TimeSpan.FromDays(90);

    /// <summary>
    /// The consent scope that lets a connection read its cards. Connections without it aren't asked for cards.
    /// </summary>
    private const string CardsScope = "cards";

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
    private readonly IOpenBankingSyncScheduler _syncScheduler;

    public OpenBankingService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        IOpenBankingApiService openBankingApiService, ILogger<OpenBankingService> logger,
        TimeProvider timeProvider, HybridCache cache, IOpenBankingSyncScheduler syncScheduler)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _openBankingApiService = openBankingApiService;
        _timeProvider = timeProvider;
        _cache = cache;
        _syncScheduler = syncScheduler;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

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

    public Task<string> BuildAuthUrlAsync(ProviderSetupUrl setupProvider, CancellationToken cancellationToken) =>
        Task.FromResult(_openBankingApiService.BuildAuthUrl(setupProvider.ProviderIds, setupProvider.Scopes));

    public async Task<bool> AddVendorViaAccessCodeAsync(AddVendor addVendor, CancellationToken cancellationToken)
    {
        ExternalOpenBankingAccessResponse providerAccessToken =
            await _openBankingApiService.ExchangeCodeForAccessTokenAsync(addVendor.AccessCode, cancellationToken);
            ExternalOpenBankingAccountConnectionResponse providerInformation =
            await _openBankingApiService.GetProviderInformation(providerAccessToken.AccessToken, cancellationToken);

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
                        await _openBankingApiService.GetProviderLogoAsync(provider.LogoUri, cancellationToken), UtcNow,
                        consentExpiresAt);
                    context.BankConnections.Add(connection);
                }
                else
                {
                    // Adding a provider that is already connected re-authenticates it, as does renewing it
                    connection.Reauthenticate(addVendor.AccessCode, scopes, UtcNow, consentExpiresAt);
                }

                connection.IssueToken(providerAccessToken.AccessToken, providerAccessToken.RefreshToken,
                    UtcNow, providerAccessToken.ExpiresIn);
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        // Synced by the background job like any other sync, so transfers are matched and the user is told when it's done
        await _syncScheduler.ScheduleUserSyncAsync(UserId, cancellationToken);

        return true;
    }

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

    public async IAsyncEnumerable<BankConnectionSummary> GetConnectionsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Names are encrypted, so they're sorted in memory
        List<BankConnection> connections = await context.BankConnections.OwnedBy(UserId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        DateTime now = UtcNow;
        foreach (BankConnection connection in connections.OrderBy(static x => x.Name))
        {
            yield return new BankConnectionSummary
            {
                ConnectionId = connection.Id,
                Name = connection.Name,
                Logo = connection.Logo,
                ConsentExpiresAt = connection.ConsentExpiresAt,
                ConsentState = ConsentStateOf(connection, now)
            };
        }
    }

    public async Task<string> GetRenewalUrlAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Only the latest token, revoked or not, renewing works for a while after consent has lapsed
        BankConnection connection = await context.BankConnections.OwnedBy(UserId)
            .AsNoTracking()
            .Include(x => x.AccessTokens.OrderByDescending(t => t.IssuedAt).Take(1))
            .SingleAsync(x => x.Id == connectionId, cancellationToken);

        return await _openBankingApiService.GetReauthenticationUrlAsync(connection.RefreshTokenForRenewal(),
            cancellationToken);
    }

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
    private DateTime ConsentExpiresAt(ExternalOpenBankingAccountConnection externalConnection)
    {
        DateTime reported = externalConnection.ConsentExpiresAt.ToUtc();
        if (reported > UtcNow)
        {
            return reported;
        }

        return UtcNow + DefaultConsentLifetime;
    }

    public async Task SyncConnectionAsync(Guid connectionId, SyncTypes syncFlags, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        BankConnection connection = await context.BankConnections.OwnedBy(UserId)
            .Include(x => x.AccessTokens.Where(t => t.RevokedAt == null))
            .SingleAsync(x => x.Id == connectionId, cancellationToken);

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

        DateTime now = UtcNow;
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

            account.UpdateIdentifier(AccountIdentifier.Create(externalAccount.AccountNumber?.SortCode,
                externalAccount.AccountNumber?.Number, externalAccount.AccountNumber?.Iban));
            context.SyncLog.Add(account.RecordSync(SyncType.Account, now));

            DateTime? transactionsFrom = lastTransactionSync.TryGetValue(account.Id, out DateTime lastSync)
                ? lastSync - TransactionSyncOverlap
                : null;

            AccountSnapshot snapshot = await FetchAccountSnapshotAsync(connection, account, authToken, syncFlags,
                transactionsFrom, cancellationToken);

            await ApplySnapshotAsync(context, account, snapshot, now, cancellationToken);
        }

        if (connection.Scopes.Contains(CardsScope))
        {
            await SyncCardsAsync(context, connection, accounts, lastTransactionSync, authToken, syncFlags, now,
                cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Syncs the connection's credit and charge cards like its accounts, each stored as a card
    /// <see cref="BankAccount"/>. Cards have no standing orders or direct debits.
    /// </summary>
    private async Task SyncCardsAsync(FinanceTrackerContext context, BankConnection connection,
        Dictionary<string, BankAccount> accounts, Dictionary<Guid, DateTime> lastTransactionSync, string authToken,
        SyncTypes syncFlags, DateTime now, CancellationToken cancellationToken)
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

            card.UpdateCard(externalCard.ToDetails());
            context.SyncLog.Add(card.RecordSync(SyncType.Account, now));

            DateTime? transactionsFrom = lastTransactionSync.TryGetValue(card.Id, out DateTime lastSync)
                ? lastSync - TransactionSyncOverlap
                : null;

            AccountSnapshot snapshot = await FetchCardSnapshotAsync(connection, card, authToken, syncFlags,
                transactionsFrom, cancellationToken);

            await ApplySnapshotAsync(context, card, snapshot, now, cancellationToken);
        }
    }

    /// <summary>
    /// Returns a usable access token, refreshing and persisting a new one when the current token has lapsed.
    /// Null when the connection has no active consent.
    /// </summary>
    private async Task<string?> GetValidAccessTokenAsync(FinanceTrackerContext context, BankConnection connection,
        CancellationToken cancellationToken)
    {
        AccessToken? activeToken = connection.ActiveToken;
        if (activeToken is null)
        {
            return null;
        }

        if (!activeToken.IsExpired(UtcNow))
        {
            return activeToken.Token;
        }

        ExternalOpenBankingAccessResponse refreshed =
            await _openBankingApiService.GetAccessTokenByRefreshTokenAsync(activeToken.RefreshToken,
                cancellationToken);

        AccessToken issued = connection.IssueToken(refreshed.AccessToken, refreshed.RefreshToken, UtcNow,
            refreshed.ExpiresIn);

        // Persist straight away, the previous refresh token is no longer valid with the provider
        await context.SaveChangesAsync(cancellationToken);

        return issued.Token;
    }

    private async Task<AccountSnapshot> FetchAccountSnapshotAsync(BankConnection connection, BankAccount account,
        string authToken, SyncTypes syncFlags, DateTime? transactionsFrom, CancellationToken cancellationToken)
    {
        string externalId = account.ExternalId;
        string accountId = account.Id.ToString();

        Task<List<ExternalOpenBankingAccountBalance>?> balances = FetchAsync(syncFlags, SyncTypes.Balance,
            () => _openBankingApiService.GetAccountBalanceAsync(externalId, authToken, cancellationToken),
            static x => x.Results,
            ex => Logger.LogErrorSyncingAccountBalance(ex, connection.Id, accountId, UserId), cancellationToken);

        Task<List<ExternalOpenBankingAccountStandingOrder>?> standingOrders = FetchAsync(syncFlags,
            SyncTypes.StandingOrders,
            () => _openBankingApiService.GetAccountStandingOrdersAsync(externalId, authToken, cancellationToken),
            static x => x.Results,
            ex => Logger.LogErrorSyncingStandingOrders(ex, connection.Id, accountId, UserId), cancellationToken);

        Task<List<ExternalOpenBankingDirectDebit>?> directDebits = FetchAsync(syncFlags, SyncTypes.DirectDebits,
            () => _openBankingApiService.GetAccountDirectDebitsAsync(externalId, authToken, cancellationToken),
            static x => x.Results,
            ex => Logger.LogErrorSyncingDirectDebits(ex, connection.Id, accountId, UserId), cancellationToken);

        Task<List<ExternalOpenBankingAccountTransaction>?> transactions = FetchAsync(syncFlags,
            SyncTypes.Transactions,
            () => _openBankingApiService.GetAccountTransactionsAsync(externalId, authToken, transactionsFrom,
                cancellationToken),
            static x => x.Results,
            ex => Logger.LogErrorSyncingTransactions(ex, connection.Id, accountId, UserId), cancellationToken);

        // Always the full pending list, stored pending rows missing from it are removed as stale
        Task<List<ExternalOpenBankingAccountTransaction>?> pendingTransactions = FetchAsync(syncFlags,
            SyncTypes.Transactions,
            () => _openBankingApiService.GetAccountPendingTransactionsAsync(externalId, authToken, null,
                cancellationToken),
            static x => x.Results,
            ex => Logger.LogErrorSyncingPendingTransactions(ex, connection.Id, accountId, UserId), cancellationToken);

        await Task.WhenAll(balances, standingOrders, directDebits, transactions, pendingTransactions);

        return new AccountSnapshot(await balances, await standingOrders, await directDebits, await transactions,
            await pendingTransactions);
    }

    private async Task<AccountSnapshot> FetchCardSnapshotAsync(BankConnection connection, BankAccount card,
        string authToken, SyncTypes syncFlags, DateTime? transactionsFrom, CancellationToken cancellationToken)
    {
        string externalId = card.ExternalId;
        string cardId = card.Id.ToString();

        Task<List<ExternalOpenBankingAccountBalance>?> balances = FetchAsync(syncFlags, SyncTypes.Balance,
            () => _openBankingApiService.GetCardBalanceAsync(externalId, authToken, cancellationToken),
            static x => x.Results,
            ex => Logger.LogErrorSyncingAccountBalance(ex, connection.Id, cardId, UserId), cancellationToken);

        Task<List<ExternalOpenBankingAccountTransaction>?> transactions = FetchAsync(syncFlags,
            SyncTypes.Transactions,
            () => _openBankingApiService.GetCardTransactionsAsync(externalId, authToken, transactionsFrom,
                cancellationToken),
            static x => x.Results,
            ex => Logger.LogErrorSyncingTransactions(ex, connection.Id, cardId, UserId), cancellationToken);

        // Always the full pending list, stored pending rows missing from it are removed as stale
        Task<List<ExternalOpenBankingAccountTransaction>?> pendingTransactions = FetchAsync(syncFlags,
            SyncTypes.Transactions,
            () => _openBankingApiService.GetCardPendingTransactionsAsync(externalId, authToken, null,
                cancellationToken),
            static x => x.Results,
            ex => Logger.LogErrorSyncingPendingTransactions(ex, connection.Id, cardId, UserId), cancellationToken);

        await Task.WhenAll(balances, transactions, pendingTransactions);

        return new AccountSnapshot(await balances, null, null, await transactions, await pendingTransactions);
    }

    /// <summary>
    /// Fetches one kind of account data. Returns null when it was not requested or the provider call failed.
    /// </summary>
    private static async Task<List<T>?> FetchAsync<TResponse, T>(SyncTypes syncFlags, SyncTypes syncType,
        Func<Task<TResponse>> fetch, Func<TResponse, IAsyncEnumerable<T>?> results, Action<Exception> onError,
        CancellationToken cancellationToken)
    {
        if (!ShouldSynchronise(syncFlags, syncType))
        {
            return null;
        }

        try
        {
            IAsyncEnumerable<T>? items = results(await fetch());
            return items is null ? [] : await items.ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            onError(ex);
            return null;
        }
    }

    private async Task ApplySnapshotAsync(FinanceTrackerContext context, BankAccount account,
        AccountSnapshot snapshot, DateTime syncedAt, CancellationToken cancellationToken)
    {
        if (snapshot.Balances is not null)
        {
            if (snapshot.Balances.FirstOrDefault() is { } balance)
            {
                AccountBalance mapped = ToBalance(account, balance);
                account.UpdateBalance(mapped.Available, mapped.Current, mapped.AsOf, mapped.CreditLimit);
            }

            context.SyncLog.Add(account.RecordSync(SyncType.Balance, syncedAt));
        }

        if (snapshot.StandingOrders is not null)
        {
            account.SyncStandingOrders(snapshot.StandingOrders.Select(static x => x.ToDetails()).ToList());
            context.SyncLog.Add(account.RecordSync(SyncType.StandingOrders, syncedAt));
        }

        if (snapshot.DirectDebits is not null)
        {
            account.SyncDirectDebits(snapshot.DirectDebits.Select(static x => x.ToDetails()).ToList());
            context.SyncLog.Add(account.RecordSync(SyncType.DirectDebits, syncedAt));
        }

        if (snapshot.Transactions is null && snapshot.PendingTransactions is null)
        {
            return;
        }

        // Pending first so a transaction reported as both is stored as booked
        Dictionary<string, TransactionDetails> incoming = [];
        foreach (ExternalOpenBankingAccountTransaction pending in snapshot.PendingTransactions ?? [])
        {
            incoming[pending.TransactionId] = await ToDetailsAsync(account, pending, true, cancellationToken);
        }

        foreach (ExternalOpenBankingAccountTransaction booked in snapshot.Transactions ?? [])
        {
            incoming[booked.TransactionId] = await ToDetailsAsync(account, booked, false, cancellationToken);
        }

        List<string> externalIds = incoming.Keys.ToList();
        Dictionary<string, BankTransaction> existing = await context.BankTransactions
            .Where(x => x.AccountId == account.Id)
            .WhereEncryptedIn(x => x.ExternalId, externalIds, context)
            .ToDictionaryAsync(static x => x.ExternalId, cancellationToken);

        foreach (TransactionDetails details in incoming.Values)
        {
            if (existing.TryGetValue(details.ExternalId, out BankTransaction? transaction))
            {
                transaction.UpdateFromSync(details);
            }
            else
            {
                context.BankTransactions.Add(BankTransaction.Import(account, details));
            }
        }

        // A failed pending fetch is null, so stored pending rows are only removed against a list we actually got
        if (snapshot.PendingTransactions is not null)
        {
            await RemoveStalePendingTransactionsAsync(context, account, incoming.Keys, cancellationToken);
        }

        // Only once booked transactions are in, the log marks where the next sync fetches from
        if (snapshot.Transactions is not null)
        {
            context.SyncLog.Add(account.RecordSync(SyncType.Transactions, syncedAt));
        }
    }

    /// <summary>
    /// The balance in our terms, a card's being what's owed as a negative amount.
    /// </summary>
    private static AccountBalance ToBalance(BankAccount account, ExternalOpenBankingAccountBalance balance)
    {
        if (account.Kind == BankAccountKind.Card)
        {
            return balance.ToCardBalance();
        }

        return balance.ToAccountBalance();
    }

    /// <summary>
    /// The transaction in our terms, a card's signed by its type.
    /// </summary>
    private static Task<TransactionDetails> ToDetailsAsync(BankAccount account,
        ExternalOpenBankingAccountTransaction transaction, bool isPending, CancellationToken cancellationToken)
    {
        if (account.Kind == BankAccountKind.Card)
        {
            return transaction.ToCardDetailsAsync(isPending, cancellationToken);
        }

        return transaction.ToDetailsAsync(isPending, cancellationToken);
    }

    /// <summary>
    /// Removes pending transactions the provider no longer reports. A bank can settle a pending transaction under a
    /// different id, which would otherwise leave the pending copy counted alongside the booked one.
    /// </summary>
    private async Task RemoveStalePendingTransactionsAsync(FinanceTrackerContext context, BankAccount account,
        IReadOnlyCollection<string> reportedExternalIds, CancellationToken cancellationToken)
    {
        // ExternalId is encrypted, so the account's pending rows are loaded and compared in memory
        List<BankTransaction> stale = (await context.BankTransactions
                .Where(x => x.AccountId == account.Id && x.IsPending)
                .ToListAsync(cancellationToken))
            .Where(x => !reportedExternalIds.Contains(x.ExternalId))
            .ToList();

        if (stale.Count == 0)
        {
            return;
        }

        context.BankTransactions.RemoveRange(stale);
        Logger.LogRemovedStalePendingTransactions(stale.Count, account.Id, UserId);
    }

    private static bool ShouldSynchronise(SyncTypes syncFlags, SyncTypes typeToCheck) =>
        syncFlags.HasFlag(SyncTypes.All) || syncFlags.HasFlag(typeToCheck);


    /// <summary>
    /// Everything fetched for one account in a sync. A null list means that data was not synced.
    /// </summary>
    private sealed record AccountSnapshot(
        List<ExternalOpenBankingAccountBalance>? Balances,
        List<ExternalOpenBankingAccountStandingOrder>? StandingOrders,
        List<ExternalOpenBankingDirectDebit>? DirectDebits,
        List<ExternalOpenBankingAccountTransaction>? Transactions,
        List<ExternalOpenBankingAccountTransaction>? PendingTransactions);
}
