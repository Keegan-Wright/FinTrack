using System.Diagnostics;
using FinanceTracker.Contracts;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;
using FinanceTracker.Telemetry;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.OpenBanking;

/// <inheritdoc cref="IAccountSnapshotFetcher"/>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IAccountSnapshotFetcher>]
public sealed class AccountSnapshotFetcher : IAccountSnapshotFetcher
{
    private readonly IOpenBankingApiService _openBankingApiService;
    private readonly ILogger<AccountSnapshotFetcher> _logger;

    /// <param name="openBankingApiService">Calls the bank's provider.</param>
    /// <param name="logger">Its logger.</param>
    public AccountSnapshotFetcher(IOpenBankingApiService openBankingApiService,
        ILogger<AccountSnapshotFetcher> logger)
    {
        _openBankingApiService = openBankingApiService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AccountSnapshot> FetchAccountAsync(BankConnection connection, BankAccount account,
        string authToken, SyncTypes syncFlags, SyncTypes enhancedAuthenticationWindowFlags, DateTime? transactionsFrom,
        ConnectionSyncTally tally, CancellationToken cancellationToken)
    {
        string externalId = account.ExternalId;
        string accountId = account.Id.ToString();

        Task<List<ExternalOpenBankingAccountBalance>?> balances = FetchAsync("Fetch balance", syncFlags,
            SyncTypes.Balance,
            () => _openBankingApiService.GetAccountBalanceAsync(externalId, authToken, cancellationToken),
            static x => x.Results,
            ex => _logger.LogErrorSyncingAccountBalance(ex, connection.Id, accountId, connection.UserId), tally,
            cancellationToken);

        Task<List<ExternalOpenBankingAccountStandingOrder>?> standingOrders = FetchAsync("Fetch standing orders",
            enhancedAuthenticationWindowFlags, SyncTypes.StandingOrders,
            () => _openBankingApiService.GetAccountStandingOrdersAsync(externalId, authToken, cancellationToken),
            static x => x.Results,
            ex => _logger.LogErrorSyncingStandingOrders(ex, connection.Id, accountId, connection.UserId), tally,
            cancellationToken);

        Task<List<ExternalOpenBankingDirectDebit>?> directDebits = FetchAsync("Fetch direct debits",
            enhancedAuthenticationWindowFlags, SyncTypes.DirectDebits,
            () => _openBankingApiService.GetAccountDirectDebitsAsync(externalId, authToken, cancellationToken),
            static x => x.Results,
            ex => _logger.LogErrorSyncingDirectDebits(ex, connection.Id, accountId, connection.UserId), tally,
            cancellationToken);

        Task<List<ExternalOpenBankingAccountTransaction>?> transactions = FetchAsync("Fetch transactions", syncFlags,
            SyncTypes.Transactions,
            () => _openBankingApiService.GetAccountTransactionsAsync(externalId, authToken, transactionsFrom,
                cancellationToken),
            static x => x.Results,
            ex => _logger.LogErrorSyncingTransactions(ex, connection.Id, accountId, connection.UserId), tally,
            cancellationToken);

        // Always the full pending list, stored pending rows missing from it are removed as stale
        Task<List<ExternalOpenBankingAccountTransaction>?> pendingTransactions = FetchAsync(
            "Fetch pending transactions", syncFlags, SyncTypes.Transactions,
            () => _openBankingApiService.GetAccountPendingTransactionsAsync(externalId, authToken, null,
                cancellationToken),
            static x => x.Results,
            ex => _logger.LogErrorSyncingPendingTransactions(ex, connection.Id, accountId, connection.UserId), tally,
            cancellationToken);

        await Task.WhenAll(balances, standingOrders, directDebits, transactions, pendingTransactions);

        return new AccountSnapshot(await balances, await standingOrders, await directDebits, await transactions,
            await pendingTransactions);
    }

    /// <inheritdoc/>
    public async Task<AccountSnapshot> FetchCardAsync(BankConnection connection, BankAccount card, string authToken,
        SyncTypes syncFlags, DateTime? transactionsFrom, ConnectionSyncTally tally,
        CancellationToken cancellationToken)
    {
        string externalId = card.ExternalId;
        string cardId = card.Id.ToString();

        Task<List<ExternalOpenBankingAccountBalance>?> balances = FetchAsync("Fetch balance", syncFlags,
            SyncTypes.Balance,
            () => _openBankingApiService.GetCardBalanceAsync(externalId, authToken, cancellationToken),
            static x => x.Results,
            ex => _logger.LogErrorSyncingAccountBalance(ex, connection.Id, cardId, connection.UserId), tally,
            cancellationToken);

        Task<List<ExternalOpenBankingAccountTransaction>?> transactions = FetchAsync("Fetch transactions", syncFlags,
            SyncTypes.Transactions,
            () => _openBankingApiService.GetCardTransactionsAsync(externalId, authToken, transactionsFrom,
                cancellationToken),
            static x => x.Results,
            ex => _logger.LogErrorSyncingTransactions(ex, connection.Id, cardId, connection.UserId), tally,
            cancellationToken);

        // Always the full pending list, stored pending rows missing from it are removed as stale
        Task<List<ExternalOpenBankingAccountTransaction>?> pendingTransactions = FetchAsync(
            "Fetch pending transactions", syncFlags, SyncTypes.Transactions,
            () => _openBankingApiService.GetCardPendingTransactionsAsync(externalId, authToken, null,
                cancellationToken),
            static x => x.Results,
            ex => _logger.LogErrorSyncingPendingTransactions(ex, connection.Id, cardId, connection.UserId), tally,
            cancellationToken);

        await Task.WhenAll(balances, transactions, pendingTransactions);

        return new AccountSnapshot(await balances, null, null, await transactions, await pendingTransactions);
    }

    /// <summary>
    /// Fetches one kind of account data in a span named <paramref name="name"/>, tagged with how many items came
    /// back. Returns null when it was not requested or the provider call failed.
    /// </summary>
    /// <typeparam name="TResponse">The provider's answer.</typeparam>
    /// <typeparam name="T">The items in it.</typeparam>
    /// <param name="name">The span's name.</param>
    /// <param name="syncFlags">What the sync fetches.</param>
    /// <param name="syncType">What this fetch is.</param>
    /// <param name="fetch">Calls the provider.</param>
    /// <param name="results">The items in its answer.</param>
    /// <param name="onError">Logs a failed fetch.</param>
    /// <param name="tally">Counts the failure.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private static async Task<List<T>?> FetchAsync<TResponse, T>(string name, SyncTypes syncFlags,
        SyncTypes syncType, Func<Task<TResponse>> fetch, Func<TResponse, IAsyncEnumerable<T>?> results,
        Action<Exception> onError, ConnectionSyncTally tally, CancellationToken cancellationToken)
    {
        if (!syncFlags.Includes(syncType))
        {
            return null;
        }

        using Activity? activity = FinTrackActivitySources.Infrastructure.StartActivity(name);
        try
        {
            IAsyncEnumerable<T>? items = results(await fetch());
            List<T> fetched = items is null ? [] : await items.ToListAsync(cancellationToken);
            activity?.SetTag(TelemetryTags.ItemCount, fetched.Count);
            return fetched;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            activity.RecordError(ex);
            tally.RecordFetchFailed();
            onError(ex);
            return null;
        }
    }
}
