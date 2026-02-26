using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using EFCore.BulkExtensions;
using FinanceTracker.Data;
using FinanceTracker.Data.Models;
using FinanceTracker.Enums;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Models.External;
using FinanceTracker.Models.Request.OpenBanking;
using FinanceTracker.Services.External.OpenBanking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Services.OpenBanking;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<IOpenBankingService>]
public class OpenBankingService : ServiceBase<OpenBankingService>, IOpenBankingService
{
    private readonly IOpenBankingApiService _openBankingApiService;
    private const int SyncMins = 5;

    public OpenBankingService(ClaimsPrincipal user,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        IOpenBankingApiService openBankingApiService, ILogger<OpenBankingService> logger) : base(user,
        financeTrackerContextFactory, logger) =>
        _openBankingApiService = openBankingApiService;

    public async IAsyncEnumerable<ExternalOpenBankingProvider> GetOpenBankingProvidersForClientAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (ExternalOpenBankingProvider provider in _openBankingApiService
                           .GetAvailableProvidersAsync(cancellationToken)
                           .OrderBy(x => x.DisplayName).WithCancellation(cancellationToken))
        {
            yield return provider;
        }
    }

    public string BuildAuthUrl(GetProviderSetupUrlRequestModel setupProviderRequestModel) =>
        _openBankingApiService.BuildAuthUrl(setupProviderRequestModel.ProviderIds,
            setupProviderRequestModel.Scopes);

    public async Task<bool> AddVendorViaAccessCodeAsync(AddVendorRequestModel addVendorRequestModel, CancellationToken cancellationToken)
    {
        ExternalOpenBankingAccessResponse providerAccessToken =
            await _openBankingApiService.ExchangeCodeForAccessTokenAsync(addVendorRequestModel.AccessCode, cancellationToken);
        ExternalOpenBankingAccountConnectionResponse providerInformation =
            await _openBankingApiService.GetProviderInformation(providerAccessToken.AccessToken, cancellationToken);

        await CreateNewProvider(addVendorRequestModel.AccessCode, providerAccessToken, providerInformation, cancellationToken);

        return true;
    }

    public async Task PerformSyncAsync(SyncTypes syncFlags, CancellationToken cancellationToken)
    {
        await foreach (OpenBankingProvider provider in GetOpenBankingProvidersAsync(cancellationToken))
        {
            await BulkLoadProviderAsync(provider, syncFlags, cancellationToken);
        }
    }

    public async Task BulkLoadProviderAsync(OpenBankingProvider provider, SyncTypes syncFlags,
        CancellationToken cancellationToken)
    {
        ICollection<OpenBankingProviderScopes> providerScopes = provider.Scopes ?? [];

        List<OpenBankingSynchronization> providerSyncs = provider.Syncronisations?.Where(x =>
            x.SyncronisationTime > DateTime.Now.AddMinutes(-SyncMins).ToUniversalTime()).ToList()?? [];

        provider.Accounts ??= [];
        provider.Scopes ??= [];
        provider.Syncronisations ??= [];


        List<OpenBankingTransaction?> transactionsForProvider = provider.Accounts?.SelectMany(x => x.Transactions!)
            .OrderByDescending(x => x.TransactionTime)
            .GroupBy(x => x.AccountId)
            .Select(x => x.FirstOrDefault()).ToList() ?? [];


        await EnsureAuthenticatedAsync(provider, cancellationToken);

        string authToken = await GetAccessTokenAsync(provider, cancellationToken);

        ExternalOpenBankingListAllAccountsResponse accountsResponse =
            await _openBankingApiService.GetAllAccountsAsync(authToken, cancellationToken);


        ConcurrentBag<ExternalOpenBankingAccount> providerAccounts = [];
        ConcurrentBag<(string AccountId, ExternalOpenBankingAccountBalance Balances)> providerBalances = [];
        ConcurrentBag<(string AccountId, ExternalOpenBankingAccountTransaction Transactions)> providerTransactions =
            [];
        ConcurrentBag<(string AccountId, ExternalOpenBankingAccountTransaction Transactions)>
            providerPendingTransactions =
                [];
        ConcurrentBag<(string AccountId, ExternalOpenBankingAccountStandingOrder StandingOrders)>
            providerStandingOrders =
                [];
        ConcurrentBag<(string AccountId, ExternalOpenBankingDirectDebit DirectDebits)> providerDirectDebits = [];
        ConcurrentBag<OpenBankingSynchronization> performedSyncs = [];

        if (accountsResponse.Results is not null)
        {
            await foreach(var account in accountsResponse.Results.WithCancellation(cancellationToken))
            {
                providerAccounts.Add(account);
                var accountScopedRelevantSyncs = providerSyncs.Where(x => x.OpenBankingAccountId == account.AccountId).ToList();

                Task<ExternalOpenBankingAccountStandingOrdersResponse?> standingOrdersTask =
                    GetOpenBankingStandingOrdersForAccountAsync(provider, syncFlags, account,
                        authToken, performedSyncs, accountScopedRelevantSyncs, cancellationToken);

                Task<ExternalOpenBankingAccountDirectDebitsResponse?> directDebitsTask =
                    GetOpenBankingDirectDebitsForAccountAsync(provider, syncFlags, account,
                        authToken, performedSyncs, accountScopedRelevantSyncs, cancellationToken);

                Task<ExternalOpenBankingGetAccountBalanceResponse?> balanceTask = GetOpenBankingAccountBalanceAsync(
                    provider, syncFlags, account, authToken,
                    performedSyncs, accountScopedRelevantSyncs, cancellationToken);

                OpenBankingTransaction? latestTransactionForAccount =
                    transactionsForProvider.FirstOrDefault(x => (x?.Account!).OpenBankingAccountId == account.AccountId);


                Task<ExternalOpenBankingAccountTransactionsResponse?> transactionsTask =
                    GetOpenBankingTransactionsForAccountAsync(provider, syncFlags, account,
                        authToken, performedSyncs, accountScopedRelevantSyncs, latestTransactionForAccount,
                        cancellationToken);

                Task<ExternalOpenBankingAccountTransactionsResponse?> pendingTransactionsTask =
                    GetOpenBankingPendingTransactionsForAccountAsync(provider, syncFlags,
                        account, authToken, performedSyncs, accountScopedRelevantSyncs, latestTransactionForAccount,
                        cancellationToken);

                await Task.WhenAll(standingOrdersTask, directDebitsTask, balanceTask, transactionsTask,
                    pendingTransactionsTask);

                ExternalOpenBankingAccountStandingOrdersResponse? standingOrdersResults = await standingOrdersTask;
                ExternalOpenBankingAccountDirectDebitsResponse? directDebitsResults = await directDebitsTask;
                ExternalOpenBankingGetAccountBalanceResponse? balanceResults = await balanceTask;
                ExternalOpenBankingAccountTransactionsResponse? transactionsResults = await transactionsTask;
                ExternalOpenBankingAccountTransactionsResponse? pendingTransactionsResults =
                    await pendingTransactionsTask;


                Task standingOrdersCollationTask = CollateAccountStandingOrdersAsync(standingOrdersResults,
                    account.AccountId, providerStandingOrders);
                Task directDebitsCollationTask =
                    CollateAccountDirectDebitsAsync(directDebitsResults, account.AccountId, providerDirectDebits);
                Task collateBalanceTask =
                    CollateAccountBalanceAsync(balanceResults, account.AccountId, providerBalances);
                Task collateTransactionsTask =
                    CollateAccountTransactionsAsync(transactionsResults, account.AccountId, providerTransactions);
                Task collatePendingTransactionsTask = CollateAccountPendingTransactionsAsync(pendingTransactionsResults,
                    account.AccountId, providerPendingTransactions);

                await Task.WhenAll(standingOrdersCollationTask, directDebitsCollationTask, collateBalanceTask,
                    collateTransactionsTask, collatePendingTransactionsTask);
            }

            await using FinanceTrackerContext context =
                await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
            IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction dbTrans = await context.Database.BeginTransactionAsync(cancellationToken);

                List<OpenBankingAccount> accountsEdits = [];
                await foreach (ExternalOpenBankingAccount account in providerAccounts.ToAsyncEnumerable()
                                   .WithCancellation(cancellationToken))
                {
                    accountsEdits.Add(UpdateOrCreateAccount(account, provider));
                }

                await context.BulkInsertOrUpdateAsync(accountsEdits, cancellationToken: cancellationToken);

                ICollection<OpenBankingAccount>? accountsForProvider = provider.Accounts;
                List<OpenBankingAccountBalance> balanceEdits = [];
                await foreach ((string AccountId, ExternalOpenBankingAccountBalance Balances) balance in
                               providerBalances.ToAsyncEnumerable().WithCancellation(cancellationToken))
                {
                    OpenBankingAccount? accountToUse =
                        accountsForProvider!.FirstOrDefault(x => x.OpenBankingAccountId == balance.AccountId);

                    balanceEdits.Add(UpdateOrCreateAccountBalance(balance.Balances, accountToUse!));
                }

                await context.BulkInsertOrUpdateAsync(balanceEdits, cancellationToken: cancellationToken);

                List<OpenBankingStandingOrder> standingOrderEdits = [];
                await foreach ((string AccountId, ExternalOpenBankingAccountStandingOrder StandingOrders) standingOrder
                               in providerStandingOrders.ToAsyncEnumerable()
                                   .WithCancellation(cancellationToken))
                {
                    OpenBankingAccount? accountToUse =
                        accountsForProvider!.FirstOrDefault(x => x.OpenBankingAccountId == standingOrder.AccountId);
                    standingOrderEdits.Add(
                        UpdateOrCreateAccountStandingOrder(standingOrder.StandingOrders, accountToUse!));
                }

                await context.BulkInsertOrUpdateAsync(standingOrderEdits, cancellationToken: cancellationToken);

                List<OpenBankingDirectDebit> directDebitEdits = [];
                await foreach ((string AccountId, ExternalOpenBankingDirectDebit DirectDebits) directDebit in
                               providerDirectDebits.ToAsyncEnumerable()
                                   .WithCancellation(cancellationToken))
                {
                    OpenBankingAccount? accountToUse =
                        accountsForProvider!.FirstOrDefault(x => x.OpenBankingAccountId == directDebit.AccountId);
                    directDebitEdits.Add(
                        UpdateOrCreateAccountDirectDebit(directDebit.DirectDebits, accountToUse!));
                }

                await context.BulkInsertOrUpdateAsync(directDebitEdits, cancellationToken: cancellationToken);


                List<OpenBankingTransaction> transactionsEdits = [];
                await foreach ((string AccountId, ExternalOpenBankingAccountTransaction Transactions) transaction in
                               providerTransactions.ToAsyncEnumerable()
                                   .WithCancellation(cancellationToken))
                {
                    OpenBankingAccount? accountToUse =
                        accountsForProvider!.FirstOrDefault(x => x.OpenBankingAccountId == transaction.AccountId);
                    transactionsEdits.Add(await UpdateOrCreateAccountTransaction(transaction.Transactions, accountToUse!,
                        false, provider));
                }

                await foreach ((string AccountId, ExternalOpenBankingAccountTransaction Transactions) transaction in
                               providerPendingTransactions.ToAsyncEnumerable()
                                   .WithCancellation(cancellationToken))
                {
                    OpenBankingAccount? accountToUse =
                        accountsForProvider!.FirstOrDefault(x => x.OpenBankingAccountId == transaction.AccountId);
                    transactionsEdits.Add(await UpdateOrCreateAccountTransaction(transaction.Transactions, accountToUse!,
                        true, provider));
                }


                await context.BulkInsertOrUpdateAsync(transactionsEdits, cancellationToken: cancellationToken);

                if (transactionsEdits.Count != 0)
                {
                    foreach (OpenBankingTransaction transaction in transactionsEdits.Where(x =>
                                 x.Classifications is not null))
                    {
                        transaction.Classifications ??= [];
                        foreach (OpenBankingTransactionClassifications classification in transaction.Classifications)
                        {
                            classification.Transaction = transaction;
                        }
                    }

                    IEnumerable<ICollection<OpenBankingTransactionClassifications>> classifications =
                        transactionsEdits.Select(x => x.Classifications)!;

                    IEnumerable<OpenBankingTransactionClassifications> classificationItems =
                        classifications.Where(x => x.Count != 0).SelectMany(x => x);

                    await context.BulkInsertOrUpdateAsync(classificationItems, cancellationToken: cancellationToken);
                }

                foreach (OpenBankingSynchronization sync in performedSyncs)
                {
                    OpenBankingAccount? account =
                        accountsForProvider!.FirstOrDefault(x => x.OpenBankingAccountId == sync.OpenBankingAccountId);

                    sync.Account = account;
                    sync.AccountId = account!.Id;
                    provider.Syncronisations.Add(sync);
                }

                await context.BulkInsertAsync(performedSyncs, cancellationToken: cancellationToken);

                await dbTrans.CommitAsync(cancellationToken);
            });
        }
    }

    private async Task<OpenBankingProvider> GetOpenBankingProviderByIdAsync(string providerId,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        OpenBankingProvider provider = await context.IsolateToUser(UserId)
            .Include(x => x.Providers)
            .SelectMany(x => x.Providers!)
            .Where(x => x.OpenBankingProviderId == providerId)
            .AsSplitQuery()
            .SingleAsync(cancellationToken);

        return provider;
    }

    private async IAsyncEnumerable<OpenBankingProvider> GetOpenBankingProvidersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (OpenBankingProvider provider in context.IsolateToUser(UserId)
                           .Include(x => x.Providers)!
                           .ThenInclude(x => x.Accounts)!
                           .ThenInclude(x => x.Transactions)!.ThenInclude(x => x.Classifications)
                           .Include(x => x.Providers)!.ThenInclude(x => x.Accounts)!.ThenInclude(x => x.AccountBalance)
                           .Include(x => x.Providers)!.ThenInclude(x => x.Scopes)
                           .Include(x => x.Providers)!.ThenInclude(x => x.Syncronisations)
                           .SelectMany(x => x.Providers!)
                           .AsSplitQuery()
                           .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return provider;
        }
    }

    private async Task CreateNewProvider(string accessCode, ExternalOpenBankingAccessResponse providerAccessToken,
        ExternalOpenBankingAccountConnectionResponse providerInformation, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        FinanceTrackerUser user = await context.IsolateToUser(UserId).SingleAsync(cancellationToken);



        await foreach (ExternalOpenBankingAccountConnection externalProvider in (providerInformation.Results ?? throw new InvalidOperationException())
                           .WithCancellation(cancellationToken))
        {
            using HttpClient logoClient = new();
            Stream providerLogo = await logoClient.GetStreamAsync(externalProvider.Provider!.LogoUri, cancellationToken);

            using MemoryStream ms = new();
            await providerLogo.CopyToAsync(ms, cancellationToken);

            OpenBankingProvider provider = new()
            {
                AccessCode = accessCode,
                Name = externalProvider.Provider.DisplayName,
                OpenBankingProviderId = externalProvider.Provider.ProviderId,
                Created = DateTime.Now.ToUniversalTime(),
                Logo = ms.ToArray(),
                Scopes = []
            };

            user.Providers ??= [];

            user.Providers.Add(provider);


            await context.SaveChangesAsync(cancellationToken);

            OpenBankingAccessToken accessToken = new()
            {
                AccessToken = providerAccessToken.AccessToken,
                ProviderId = provider.Id,
                ExpiresIn = providerAccessToken.ExpiresIn,
                RefreshToken = providerAccessToken.RefreshToken,
                Created = DateTime.Now.ToUniversalTime()
            };

            await foreach (string scope in externalProvider.Scopes!.WithCancellation(cancellationToken))
            {
                OpenBankingProviderScopes providerScope = new()
                {
                    Scope = scope, Created = DateTime.Now.ToUniversalTime(), ProviderId = provider.Id
                };

                provider.Scopes.Add(providerScope);
            }

            user.OpenBankingAccessTokens ??= [];
            user.OpenBankingAccessTokens.Add(accessToken);
            await context.SaveChangesAsync(cancellationToken);


            await BulkLoadProviderAsync(provider, SyncTypes.All, cancellationToken);
        }
    }


    private static async Task CollateAccountPendingTransactionsAsync(
        ExternalOpenBankingAccountTransactionsResponse? pendingTransactionsResults, string accountId,
        ConcurrentBag<(string, ExternalOpenBankingAccountTransaction)> providerPendingTransactions)
    {
        if (pendingTransactionsResults?.Results != null)
        {
            await foreach (ExternalOpenBankingAccountTransaction pendingTransaction in pendingTransactionsResults
                               .Results)
            {
                providerPendingTransactions.Add((accountId, pendingTransaction));
            }
        }
    }

    private static async Task CollateAccountTransactionsAsync(
        ExternalOpenBankingAccountTransactionsResponse? transactionsResults, string accountId,
        ConcurrentBag<(string, ExternalOpenBankingAccountTransaction)> providerTransactions)
    {
        if (transactionsResults?.Results != null)
        {
            await foreach (ExternalOpenBankingAccountTransaction transaction in transactionsResults.Results)
            {
                providerTransactions.Add((accountId, transaction));
            }
        }
    }

    private static async Task CollateAccountBalanceAsync(ExternalOpenBankingGetAccountBalanceResponse? balanceResults,
        string accountId, ConcurrentBag<(string, ExternalOpenBankingAccountBalance)> providerBalances)
    {
        if (balanceResults?.Results != null)
        {
            await foreach (ExternalOpenBankingAccountBalance accountBalance in balanceResults.Results)
            {
                providerBalances.Add((accountId, accountBalance));
            }
        }
    }

    private static async Task CollateAccountDirectDebitsAsync(
        ExternalOpenBankingAccountDirectDebitsResponse? directDebitsResults, string accountId,
        ConcurrentBag<(string AccountId, ExternalOpenBankingDirectDebit DirectDebits)> providerDirectDebits)
    {
        if (directDebitsResults?.Results != null)
        {
            await foreach (ExternalOpenBankingDirectDebit directDebit in directDebitsResults.Results)
            {
                providerDirectDebits.Add((accountId, directDebit));
            }
        }
    }

    private static async Task CollateAccountStandingOrdersAsync(
        ExternalOpenBankingAccountStandingOrdersResponse? standingOrdersResults, string accountId,
        ConcurrentBag<(string AccountId, ExternalOpenBankingAccountStandingOrder StandingOrders)>
            providerStandingOrders)
    {
        if (standingOrdersResults?.Results != null)
        {
            await foreach (ExternalOpenBankingAccountStandingOrder standingOrder in standingOrdersResults.Results)
            {
                providerStandingOrders.Add((accountId, standingOrder));
            }
        }
    }

    private async Task<ExternalOpenBankingAccountStandingOrdersResponse?> GetOpenBankingStandingOrdersForAccountAsync(
        OpenBankingProvider provider, SyncTypes syncFlags, ExternalOpenBankingAccount account, string authToken,
        ConcurrentBag<OpenBankingSynchronization> performedSyncs, IEnumerable<OpenBankingSynchronization> relevantSyncs,
        CancellationToken cancellationToken)
    {
        if (!ShouldSynchronise(syncFlags, relevantSyncs, SyncTypes.StandingOrders))
        {
            return null;
        }

        try
        {
            ExternalOpenBankingAccountStandingOrdersResponse standingOrders =
                await _openBankingApiService.GetAccountStandingOrdersAsync(account.AccountId, authToken,
                    cancellationToken);
            if (standingOrders.Results != null &&
                (await standingOrders.Results.ToListAsync(cancellationToken)).Count != 0)
            {
                performedSyncs.Add(CreateSyncLog(provider, account.AccountId, SyncTypes.StandingOrders));
            }

            return standingOrders;
        }
        catch (Exception)
        {
            Logger.LogErrorSyncingStandingOrders(provider.Id, provider.Accounts?.FirstOrDefault(x => x.OpenBankingAccountId == account.AccountId)?.Id.ToString() ?? "No Account", UserId);
            return new ExternalOpenBankingAccountStandingOrdersResponse();
        }

    }

    private async Task<ExternalOpenBankingAccountDirectDebitsResponse?> GetOpenBankingDirectDebitsForAccountAsync(
        OpenBankingProvider provider, SyncTypes syncFlags, ExternalOpenBankingAccount account, string authToken,
        ConcurrentBag<OpenBankingSynchronization> performedSyncs, IEnumerable<OpenBankingSynchronization> relevantSyncs,
        CancellationToken cancellationToken)
    {
        if (!ShouldSynchronise(syncFlags, relevantSyncs, SyncTypes.DirectDebits))
        {
            return null;
        }

        try
        {
            ExternalOpenBankingAccountDirectDebitsResponse directDebits =
                await _openBankingApiService.GetAccountDirectDebitsAsync(account.AccountId, authToken,
                    cancellationToken);
            if (directDebits.Results != null && (await directDebits.Results.ToListAsync(cancellationToken)).Count != 0)
            {
                performedSyncs.Add(CreateSyncLog(provider, account.AccountId, SyncTypes.DirectDebits));
            }

            return directDebits;
        }
        catch (Exception)
        {
            Logger.LogErrorSyncingDirectDebits(provider.Id, provider.Accounts?.FirstOrDefault(x => x.OpenBankingAccountId == account.AccountId)?.Id.ToString() ?? "No Account", UserId);
            return new ExternalOpenBankingAccountDirectDebitsResponse();
        }
    }

    private async Task<ExternalOpenBankingGetAccountBalanceResponse?> GetOpenBankingAccountBalanceAsync(
        OpenBankingProvider provider, SyncTypes syncFlags, ExternalOpenBankingAccount account, string authToken,
        ConcurrentBag<OpenBankingSynchronization> performedSyncs, IEnumerable<OpenBankingSynchronization> relevantSyncs,
        CancellationToken cancellationToken)
    {
        if (!ShouldSynchronise(syncFlags, relevantSyncs, SyncTypes.Balance))
        {
            return null;
        }

        try
        {
            ExternalOpenBankingGetAccountBalanceResponse balance =
                await _openBankingApiService.GetAccountBalanceAsync(account.AccountId, authToken, cancellationToken);
            if (balance.Results != null && (await balance.Results.ToListAsync(cancellationToken)).Count != 0)
            {
                performedSyncs.Add(CreateSyncLog(provider, account.AccountId, SyncTypes.Balance));
            }

            return balance;
        }
        catch (Exception)
        {
            Logger.LogErrorSyncingAccountBalance(provider.Id, provider.Accounts?.FirstOrDefault(x => x.OpenBankingAccountId == account.AccountId)?.Id.ToString() ?? "No Account", UserId);
            return new ();
        }

    }

    private async Task<ExternalOpenBankingAccountTransactionsResponse?> GetOpenBankingTransactionsForAccountAsync(
        OpenBankingProvider provider, SyncTypes syncFlags, ExternalOpenBankingAccount account, string authToken,
        ConcurrentBag<OpenBankingSynchronization> performedSyncs, IEnumerable<OpenBankingSynchronization> relevantSyncs,
        OpenBankingTransaction? latestTransactionForAccount, CancellationToken cancellationToken)
    {
        if (!ShouldSynchronise(syncFlags, relevantSyncs, SyncTypes.Transactions))
        {
            return null;
        }

        try
        {
            ExternalOpenBankingAccountTransactionsResponse transactions =
                await _openBankingApiService.GetAccountTransactionsAsync(account.AccountId, authToken,
                    latestTransactionForAccount?.TransactionTime, cancellationToken);
            if (transactions.Results != null && (await transactions.Results.ToListAsync(cancellationToken)).Count != 0)
            {
                performedSyncs.Add(CreateSyncLog(provider, account.AccountId, SyncTypes.Transactions));
            }

            return transactions;
        }
        catch (Exception)
        {
            Logger.LogErrorSyncingTransactions(provider.Id,
                provider.Accounts?.FirstOrDefault(x => x.OpenBankingAccountId == account.AccountId)?.Id.ToString() ??
                "No Account", UserId);
            return new();
        }

    }

    private async Task<ExternalOpenBankingAccountTransactionsResponse?>
        GetOpenBankingPendingTransactionsForAccountAsync(OpenBankingProvider provider, SyncTypes syncFlags,
            ExternalOpenBankingAccount account, string authToken,
            ConcurrentBag<OpenBankingSynchronization> performedSyncs,
            IEnumerable<OpenBankingSynchronization> relevantSyncs, OpenBankingTransaction? latestTransactionForAccount,
            CancellationToken cancellationToken)
    {
        if (!ShouldSynchronise(syncFlags, relevantSyncs, SyncTypes.Transactions))
        {
            return null;
        }

        try
        {

            ExternalOpenBankingAccountTransactionsResponse transactions =
                await _openBankingApiService.GetAccountPendingTransactionsAsync(account.AccountId,
                    authToken, latestTransactionForAccount?.TransactionTime, cancellationToken);
            if (transactions.Results != null && (await transactions.Results.ToListAsync(cancellationToken)).Count != 0)
            {
                performedSyncs.Add(CreateSyncLog(provider, account.AccountId, SyncTypes.PendingTransactions));
            }

            return transactions;
        }
        catch (Exception)
        {
            Logger.LogErrorSyncingPendingTransactions(provider.Id,
                provider.Accounts?.FirstOrDefault(x => x.OpenBankingAccountId == account.AccountId)?.Id.ToString() ??
                "No Account", UserId);
            return new();
        }

    }

    private static OpenBankingSynchronization
        CreateSyncLog(OpenBankingProvider provider, string accountId, SyncTypes syncType) =>
        new()
        {
            Created = DateTime.Now.ToUniversalTime(),
            SyncronisationTime = DateTime.Now.ToUniversalTime(),
            SyncronisationType = (int)syncType,
            ProviderId = provider.Id,
            Provider = provider,
            OpenBankingAccountId = accountId,
            Id = Guid.NewGuid()
        };

    private static bool ShouldSynchronise(SyncTypes syncFlags, IEnumerable<OpenBankingSynchronization> relevantSyncs,
        SyncTypes typeToCheck)
    {
        return (syncFlags.HasFlag(SyncTypes.All) || syncFlags.HasFlag(typeToCheck)) &&
               !relevantSyncs.Any(x => x.SyncronisationType == (int)typeToCheck);
    }

    private async Task<string> GetAccessTokenAsync(OpenBankingProvider provider, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        OpenBankingAccessToken? accessToken = await context.IsolateToUser(UserId)
            .Include(x => x.OpenBankingAccessTokens)
            .AsNoTracking()
            .SelectMany(x => x.OpenBankingAccessTokens!)
            .Where(x => x.ProviderId == provider.Id)
            .OrderByDescending(x => x.Created)
            .FirstOrDefaultAsync(cancellationToken);

        if (accessToken!.Created.AddSeconds(accessToken.ExpiresIn) > DateTime.Now.ToUniversalTime())
        {
            return accessToken.AccessToken;
        }

        ExternalOpenBankingAccessResponse refreshTokenResponse =
            await _openBankingApiService.GetAccessTokenByRefreshTokenAsync(
                accessToken.RefreshToken, cancellationToken);

        OpenBankingAccessToken newAccessToken = new()
        {
            AccessToken = refreshTokenResponse.AccessToken,
            ExpiresIn = refreshTokenResponse.ExpiresIn,
            ProviderId = provider.Id,
            RefreshToken = refreshTokenResponse.RefreshToken,
            Created = DateTime.Now.ToUniversalTime()
        };

        await context.AddAsync(newAccessToken, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return refreshTokenResponse.AccessToken;
    }

    private async Task EnsureAuthenticatedAsync(OpenBankingProvider provider, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        // User hasn't authenticated yet
        if (!await context.IsolateToUser(UserId)
                .AsNoTracking()
                .Include(x => x.OpenBankingAccessTokens)
                .Where(x => x.OpenBankingAccessTokens!.Any(c => c.ProviderId == provider.Id)).AnyAsync(cancellationToken))
        {
            ExternalOpenBankingAccessResponse response =
                await _openBankingApiService.ExchangeCodeForAccessTokenAsync(
                    provider.AccessCode, cancellationToken);

            OpenBankingAccessToken accessToken = new()
            {
                AccessToken = response.AccessToken,
                ExpiresIn = response.ExpiresIn,
                ProviderId = provider.Id,
                RefreshToken = response.RefreshToken,
                Created = DateTime.Now.ToUniversalTime()
            };

            await context.AddAsync(accessToken, cancellationToken);
        }
    }

    private static OpenBankingAccountBalance UpdateOrCreateAccountBalance(
        ExternalOpenBankingAccountBalance balance, OpenBankingAccount account)
    {
        OpenBankingAccountBalance? trackedBalance = account.AccountBalance;

        if (trackedBalance is not null)
        {
            trackedBalance.Current = balance.Current;
            trackedBalance.Available = balance.Available;
            trackedBalance.Updated = DateTime.Now.ToUniversalTime();
        }
        else
        {
            OpenBankingAccountBalance newAccountBalance = new()
            {
                Available = balance.Available,
                Currency = balance.Currency,
                Current = balance.Current,
                Created = DateTime.Now.ToUniversalTime(),
                AccountId = account.Id,
                Account = account,
                Id = Guid.NewGuid()
            };
            account.AccountBalance = newAccountBalance;

            return newAccountBalance;
        }

        return trackedBalance;
    }

    private static async Task<OpenBankingTransaction> UpdateOrCreateAccountTransaction(
        ExternalOpenBankingAccountTransaction transaction, OpenBankingAccount account, bool isPendingTransaction,
        OpenBankingProvider provider)
    {
        account.Transactions ??= [];
        provider.Transactions ??= [];

        OpenBankingTransaction? trackedTransaction =
            account.Transactions.FirstOrDefault(x => x.TransactionId == transaction.TransactionId);

        if (trackedTransaction is not null)
        {
            trackedTransaction.TransactionType = transaction.TransactionType;
            trackedTransaction.TransactionId = transaction.TransactionId;
            trackedTransaction.Currency = transaction.Currency;
            trackedTransaction.TransactionCategory = transaction.TransactionCategory;
            trackedTransaction.Amount = trackedTransaction.Amount;
            trackedTransaction.Created = trackedTransaction.Created;
            trackedTransaction.Description = trackedTransaction.Description;
            trackedTransaction.Updated = DateTime.Now.ToUniversalTime();
            trackedTransaction.Pending = isPendingTransaction;
            trackedTransaction.TransactionTime = transaction.Timestamp;
        }
        else
        {
            OpenBankingTransaction newTransaction = new()
            {
                TransactionType = transaction.TransactionType,
                TransactionId = transaction.TransactionId,
                Currency = transaction.Currency,
                Amount = transaction.Amount,
                Description = transaction.Description,
                Created = DateTime.Now.ToUniversalTime(),
                TransactionCategory = transaction.TransactionCategory,
                TransactionTime = transaction.Timestamp,
                Pending = isPendingTransaction,
                ProviderId = provider.Id,
                AccountId = account.Id,
                Account = account,
                Id = Guid.NewGuid()
            };
            provider.Transactions.Add(newTransaction);
            account.Transactions.Add(newTransaction);

            newTransaction.Classifications ??= [];

            await foreach (string classification in transaction.TransactionClassification!)
            {
                newTransaction.Classifications.Add(new OpenBankingTransactionClassifications
                {
                    Classification = classification, TransactionId = newTransaction.Id, Id = Guid.NewGuid()
                });
            }

            return newTransaction;
        }

        return trackedTransaction;
    }

    private static OpenBankingAccount UpdateOrCreateAccount(ExternalOpenBankingAccount account, OpenBankingProvider provider)
    {
        OpenBankingAccount? trackedAccount =
            provider.Accounts!.FirstOrDefault(x => x.OpenBankingAccountId == account.AccountId);

        if (trackedAccount is not null)
        {
            trackedAccount.Updated = DateTime.Now.ToUniversalTime();
        }
        else
        {
            OpenBankingAccount newAccount = new()
            {
                OpenBankingAccountId = account.AccountId,
                Id = Guid.NewGuid(),
                AccountType = account.AccountType,
                Currency = account.Currency,
                DisplayName = account.DisplayName,
                Created = DateTime.Now.ToUniversalTime(),
                ProviderId = provider.Id
            };
            provider.Accounts!.Add(newAccount);

            return newAccount;
        }

        return trackedAccount;
    }

    private static OpenBankingStandingOrder UpdateOrCreateAccountStandingOrder(
        ExternalOpenBankingAccountStandingOrder standingOrder, OpenBankingAccount account)
    {
        account.StandingOrders ??= [];

        OpenBankingStandingOrder? trackedStandingOrder = account.StandingOrders.FirstOrDefault(x =>
            x.Reference == standingOrder.Reference && x.Payee == standingOrder.Payee);

        if (trackedStandingOrder is not null)
        {
            trackedStandingOrder.Frequency = standingOrder.Frequency;
            trackedStandingOrder.Currency = standingOrder.Currency;
            trackedStandingOrder.Reference = standingOrder.Reference;
            trackedStandingOrder.Payee = standingOrder.Payee;
            trackedStandingOrder.FinalPaymentAmount = standingOrder.FinalPaymentAmount;
            trackedStandingOrder.FirstPaymentDate = standingOrder.FirstPaymentDate;
            trackedStandingOrder.NextPaymentDate = standingOrder.NextPaymentDate;
            trackedStandingOrder.FinalPaymentDate = standingOrder.FinalPaymentDate;
            trackedStandingOrder.NextPaymentAmount = standingOrder.NextPaymentAmount;
            trackedStandingOrder.Status = standingOrder.Status;
            trackedStandingOrder.Updated = DateTime.Now.ToUniversalTime();
            trackedStandingOrder.Timestamp = standingOrder.Timestamp;
            trackedStandingOrder.FirstPaymentAmount = standingOrder.FirstPaymentAmount;
        }
        else
        {
            OpenBankingStandingOrder newStandingOrder = new()
            {
                Currency = standingOrder.Currency,
                FinalPaymentAmount = standingOrder.FinalPaymentAmount,
                FinalPaymentDate = standingOrder.FinalPaymentDate,
                FirstPaymentAmount = standingOrder.FirstPaymentAmount,
                FirstPaymentDate = standingOrder.FirstPaymentDate,
                Frequency = standingOrder.Frequency,
                NextPaymentAmount = standingOrder.NextPaymentAmount,
                NextPaymentDate = standingOrder.NextPaymentDate,
                Payee = standingOrder.Payee,
                Reference = standingOrder.Reference,
                Status = standingOrder.Status,
                Timestamp = standingOrder.Timestamp,
                Created = DateTime.Now.ToUniversalTime(),
                AccountId = account.Id,
                Account = account,
                Id = Guid.NewGuid()
            };
            account.StandingOrders.Add(newStandingOrder);
            return newStandingOrder;
        }

        return trackedStandingOrder;
    }

    private static OpenBankingDirectDebit UpdateOrCreateAccountDirectDebit(ExternalOpenBankingDirectDebit directDebit,
        OpenBankingAccount account)
    {
        account.DirectDebits ??= [];
        OpenBankingDirectDebit? trackedDirectDebit =
            account.DirectDebits.FirstOrDefault(x => x.OpenBankingDirectDebitId == directDebit.DirectDebitId);

        if (trackedDirectDebit is not null)
        {
            trackedDirectDebit.OpenBankingDirectDebitId = directDebit.DirectDebitId;
            trackedDirectDebit.TimeStamp = directDebit.Timestamp;
            trackedDirectDebit.Name = directDebit.Name;
            trackedDirectDebit.Status = directDebit.Status;
            trackedDirectDebit.PreviousPaymentTimeStamp = directDebit.PreviousPaymentTimestamp;
            trackedDirectDebit.PreviousPaymentAmount = directDebit.PreviousPaymentAmount;
            trackedDirectDebit.Currency = directDebit.Currency;
            trackedDirectDebit.Updated = DateTime.Now.ToUniversalTime();
        }
        else
        {
            OpenBankingDirectDebit newDirectDebit = new()
            {
                Currency = directDebit.Currency,
                Name = directDebit.Name,
                OpenBankingDirectDebitId = directDebit.DirectDebitId,
                PreviousPaymentAmount = directDebit.PreviousPaymentAmount,
                PreviousPaymentTimeStamp = directDebit.PreviousPaymentTimestamp,
                Status = directDebit.Status,
                TimeStamp = directDebit.Timestamp,
                Created = DateTime.Now.ToUniversalTime(),
                AccountId = account.Id,
                Account = account,
                Id = Guid.NewGuid()
            };

            account.DirectDebits.Add(newDirectDebit);
            return newDirectDebit;
        }

        return trackedDirectDebit;
    }
}
