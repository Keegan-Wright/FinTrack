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

namespace FinanceTracker.Services.OpenBanking;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<IOpenBankingService>]
public class OpenBankingService : ServiceBase, IOpenBankingService
{
    private readonly IOpenBankingApiService _openBankingApiService;
    private readonly int _syncMins = 5;
    
    public OpenBankingService(ClaimsPrincipal user, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        IOpenBankingApiService openBankingApiService) : base(user, financeTrackerContextFactory)
    {
        _openBankingApiService = openBankingApiService;
    }

    public async IAsyncEnumerable<ExternalOpenBankingProvider> GetOpenBankingProvidersForClientAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var provider in _openBankingApiService.GetAvailableProvidersAsync(cancellationToken)
                           .OrderBy(x => x.DisplayName).WithCancellation(cancellationToken))
            yield return provider;
    }

    public string BuildAuthUrl(GetProviderSetupUrlRequestModel setupProviderRequestModel,
        CancellationToken cancellationToken)
    {
        return _openBankingApiService.BuildAuthUrl(setupProviderRequestModel.ProviderIds,
            setupProviderRequestModel.Scopes);
    }

    public async Task<bool> AddVendorViaAccessCodeAsync(string accessCode, CancellationToken cancellationToken)
    {
        var providerAccessToken =
            await _openBankingApiService.ExchangeCodeForAccessTokenAsync(accessCode, cancellationToken);
        var providerInformation =
            await _openBankingApiService.GetProviderInformation(providerAccessToken.AccessToken, cancellationToken);

        await CreateNewProvider(accessCode, providerAccessToken, providerInformation, cancellationToken);

        return true;
    }

    public async Task PerformSyncAsync(SyncTypes syncFlags, CancellationToken cancellationToken)
    {
        await foreach (var provider in GetOpenBankingProvidersAsync(cancellationToken))
            await BulkLoadProviderAsync(provider, syncFlags, cancellationToken);
    }

    private async Task<OpenBankingProvider> GetOpenBankingProviderByIdAsync(string providerId,
        CancellationToken cancellationToken)
    {
        await using var context = await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        var provider = await context.IsolateToUser(UserId)
            .Include(x => x.Providers)
            .SelectMany(x => x.Providers)
            .Where(x => x.OpenBankingProviderId == providerId)
            .AsSplitQuery()
            .SingleAsync(cancellationToken);

        return provider;
    }

    private async IAsyncEnumerable<OpenBankingProvider> GetOpenBankingProvidersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var context = await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (var provider in context.IsolateToUser(UserId)
                           .Include(x => x.Providers)
                           .ThenInclude(x => x.Accounts)
                           .ThenInclude(x => x.Transactions)
                           .Include(x => x.Providers).ThenInclude(x => x.Accounts).ThenInclude(x => x.AccountBalance)
                           .Include(x => x.Providers).ThenInclude(x => x.Scopes)
                           .Include(x => x.Providers).ThenInclude(x => x.Syncronisations)
                           .SelectMany(x => x.Providers)
                           .AsSplitQuery()
                           .AsAsyncEnumerable().WithCancellation(cancellationToken))
            yield return provider;
    }

    private async Task CreateNewProvider(string accessCode, ExternalOpenBankingAccessResponse providerAccessToken,
        ExternalOpenBankingAccountConnectionResponse providerInformation, CancellationToken cancellationToken)
    {
        await using var context = await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await context.IsolateToUser(UserId).SingleAsync(cancellationToken);
        await foreach (var externalProvider in providerInformation.Results.WithCancellation(cancellationToken))
        {
            using var logoClient = new HttpClient();
            var providerLogo = await logoClient.GetStreamAsync(externalProvider.Provider.LogoUri, cancellationToken);

            using var ms = new MemoryStream();
            await providerLogo.CopyToAsync(ms, cancellationToken);

            var provider = new OpenBankingProvider
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

            var accessToken = new OpenBankingAccessToken
            {
                AccessToken = providerAccessToken.AccessToken,
                ProviderId = provider.Id,
                ExpiresIn = providerAccessToken.ExpiresIn,
                RefreshToken = providerAccessToken.RefreshToken,
                Created = DateTime.Now.ToUniversalTime()
            };

            await foreach (var scope in externalProvider.Scopes.WithCancellation(cancellationToken))
            {
                var providerScope = new OpenBankingProviderScopes
                {
                    Scope = scope,
                    Created = DateTime.Now.ToUniversalTime(),
                    ProviderId = provider.Id
                };

                provider.Scopes.Add(providerScope);
            }

            await context.AddAsync(accessToken, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);


            await BulkLoadProviderAsync(provider, SyncTypes.All, cancellationToken);
        }
    }
    
    private async Task BulkLoadProviderAsync(OpenBankingProvider provider, SyncTypes syncFlags, CancellationToken cancellationToken)
        {
            await using var context = await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
            var providerScopes = provider.Scopes ?? [];
            
            var providerSyncs = provider.Syncronisations?.Where(x =>
                x.SyncronisationTime > DateTime.Now.AddMinutes(-_syncMins).ToUniversalTime()) ?? [];

            provider.Accounts ??= [];
            provider.Scopes ??= [];
            provider.Syncronisations ??= [];
          
            var transactionsForProvider = provider.Accounts?.SelectMany(x => x.Transactions)
                .OrderByDescending(x => x.TransactionTime)
                .GroupBy(x => x.AccountId)
                .Select(x => x.FirstOrDefault()).ToList() ?? [];


            await EnsureAuthenticatedAsync(provider, cancellationToken);

            var authToken = await GetAccessTokenAsync(provider, cancellationToken);

            var accountsResponse = await _openBankingApiService.GetAllAccountsAsync(authToken, cancellationToken);


            var providerAccounts = new ConcurrentBag<ExternalOpenBankingAccount>();
            var providerBalances = new ConcurrentBag<(string AccountId, ExternalOpenBankingAccountBalance Balances)>();
            var providerTransactions = new ConcurrentBag<(string AccountId, ExternalOpenBankingAccountTransaction Transactions)>();
            var providerPendingTransactions = new ConcurrentBag<(string AccountId , ExternalOpenBankingAccountTransaction Transactions)>();
            var providerStandingOrders = new ConcurrentBag<(string AccountId, ExternalOpenBankingAccountStandingOrder StandingOrders)>();
            var providerDirectDebits = new ConcurrentBag<(string AccountId , ExternalOpenBankingDirectDebit DirectDebits)>();
            var performedSyncs = new ConcurrentBag<OpenBankingSynchronization>();

            await Parallel.ForEachAsync(accountsResponse.Results,  cancellationToken: cancellationToken,async (account, cts) =>
            {
                providerAccounts.Add(account);

                var relevantSyncs = providerSyncs.Where(x => x.OpenBankingAccountId == account.AccountId);
                using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts);

                var openBankingSynchronizations = relevantSyncs.ToList();
                var standingOrdersTask = GetOpenBankingStandingOrdersForAccountAsync(provider, syncFlags, account, authToken, performedSyncs, openBankingSynchronizations, source.Token);
                var directDebitsTask = GetOpenBankingDirectDebitsForAccountAsync(provider, syncFlags, account, authToken, performedSyncs, openBankingSynchronizations, source.Token);
                var balanceTask = GetOpenBankingAccountBalanceAsync(provider, syncFlags, account, authToken, performedSyncs, openBankingSynchronizations,  source.Token);

                var latestTransactionForAccount = transactionsForProvider.FirstOrDefault(x => x?.Account.OpenBankingAccountId == account.AccountId);

  
                var transactionsTask = GetOpenBankingTransactionsForAccountAsync(provider, syncFlags, account, authToken, performedSyncs, openBankingSynchronizations, latestTransactionForAccount, source.Token);
                var pendingTransactionsTask = GetOpenBankingPendingTransactionsForAccountAsync(provider, syncFlags, account, authToken, performedSyncs, openBankingSynchronizations, latestTransactionForAccount, source.Token);

                await Task.WhenAll(standingOrdersTask, directDebitsTask, balanceTask, transactionsTask, pendingTransactionsTask);

                var standingOrdersResults = await standingOrdersTask;
                var directDebitsResults = await directDebitsTask;
                var balanceResults = await balanceTask;
                var transactionsResults = await transactionsTask;
                var pendingTransactionsResults = await pendingTransactionsTask;

                var standingOrdersCollationTask = CollateAccountStandingOrdersAsync(standingOrdersResults, account.AccountId, providerStandingOrders);
                var directDebitsCollationTask = CollateAccountDirectDebitsAsync(directDebitsResults, account.AccountId, providerDirectDebits);
                var collateBalanceTask = CollateAccountBalanceAsync(balanceResults, account.AccountId, providerBalances);
                var collateTransactionsTask = CollateAccountTransactionsAsync(transactionsResults, account.AccountId, providerTransactions);
                var collatePendingTransactionsTask = CollateAccountPendingTransactionsAsync(pendingTransactionsResults, account.AccountId, providerPendingTransactions);

                await Task.WhenAll(standingOrdersCollationTask, directDebitsCollationTask, collateBalanceTask, collateTransactionsTask, collatePendingTransactionsTask);

            });

            var strategy = context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
     
                using var dbTrans = await context.Database.BeginTransactionAsync(cancellationToken);

                var accountsEdits = new List<OpenBankingAccount>();
                await foreach (var account in providerAccounts.ToAsyncEnumerable().WithCancellation(cancellationToken))
                {
                    accountsEdits.Add(UpdateOrCreateAccount(account, provider));
                }

                await context.BulkInsertOrUpdateAsync(accountsEdits, cancellationToken: cancellationToken);

                var accountsForProvider = provider.Accounts;
                var balanceEdits = new List<OpenBankingAccountBalance>();
                await foreach (var balance in providerBalances.ToAsyncEnumerable().WithCancellation(cancellationToken))
                {
                    var accountToUse =
                        accountsForProvider.FirstOrDefault(x => x.OpenBankingAccountId == balance.AccountId);
                    
                    balanceEdits.Add(UpdateOrCreateAccountBalance(balance.Balances, accountToUse));
                }

                await context.BulkInsertOrUpdateAsync(balanceEdits, cancellationToken: cancellationToken);

                var standingOrderEdits = new List<OpenBankingStandingOrder>();
                await foreach (var standingOrder in providerStandingOrders.ToAsyncEnumerable().WithCancellation(cancellationToken))
                {
                    var accountToUse =
                        accountsForProvider.FirstOrDefault(x => x.OpenBankingAccountId == standingOrder.AccountId);
                    standingOrderEdits.Add(
                        UpdateOrCreateAccountStandingOrder(standingOrder.StandingOrders, accountToUse));
                }

                await context.BulkInsertOrUpdateAsync(standingOrderEdits, cancellationToken: cancellationToken);

                var directDebitEdits = new List<OpenBankingDirectDebit>();
                await foreach (var directDebit in providerDirectDebits.ToAsyncEnumerable().WithCancellation(cancellationToken))
                {
                    var accountToUse =
                        accountsForProvider.FirstOrDefault(x => x.OpenBankingAccountId == directDebit.AccountId);
                    directDebitEdits.Add(
                        UpdateOrCreateAccountDirectDebit(directDebit.DirectDebits, accountToUse));
                }

                await context.BulkInsertOrUpdateAsync(directDebitEdits, cancellationToken: cancellationToken);


                var transactionsEdits = new List<OpenBankingTransaction>();
                await foreach (var transaction in providerTransactions.ToAsyncEnumerable().WithCancellation(cancellationToken))
                {
                    var accountToUse =
                        accountsForProvider.FirstOrDefault(x => x.OpenBankingAccountId == transaction.AccountId);
                    transactionsEdits.Add(await UpdateOrCreateAccountTransaction(transaction.Transactions, accountToUse,
                        false, provider));
                }

                await foreach (var transaction in providerPendingTransactions.ToAsyncEnumerable().WithCancellation(cancellationToken))
                {
                    var accountToUse =
                        accountsForProvider.FirstOrDefault(x => x.OpenBankingAccountId == transaction.AccountId);
                    transactionsEdits.Add(await UpdateOrCreateAccountTransaction(transaction.Transactions, accountToUse,
                        true, provider));
                }


                await context.BulkInsertOrUpdateAsync(transactionsEdits, cancellationToken: cancellationToken);

                if (transactionsEdits.Any())
                {
                    foreach (var transaction in transactionsEdits.Where(x => x.Classifications is not null))
                    {
                        transaction.Classifications ??= [];
                        foreach (var classification in transaction.Classifications)
                        {
                            classification.Transaction = transaction;
                        }
                    }

                    var classifications = transactionsEdits.Select(x => x.Classifications);

                    var classificationItems = classifications.Where(x => x is not null && x.Any()).SelectMany(x => x);

                    await context.BulkInsertOrUpdateAsync(classificationItems, cancellationToken: cancellationToken);

                }

                foreach (var sync in performedSyncs)
                {
                    var account =
                        accountsForProvider.FirstOrDefault(x => x.OpenBankingAccountId == sync.OpenBankingAccountId);

                    sync.Account = account;
                    sync.AccountId = account.Id;
                    provider.Syncronisations.Add(sync);
                }

                await context.BulkInsertAsync(performedSyncs, cancellationToken: cancellationToken);

                await dbTrans.CommitAsync(cancellationToken);
            });
           

        }

        private async Task CollateAccountPendingTransactionsAsync(ExternalOpenBankingAccountTransactionsResponse? pendingTransactionsResults, string accountId, ConcurrentBag<(string, ExternalOpenBankingAccountTransaction)> providerPendingTransactions)
        {
            if (pendingTransactionsResults == null)
            {
                return;
            }
            else if(pendingTransactionsResults.Results != null)
            {

                await foreach (var pendingTransaction in pendingTransactionsResults.Results)
                {
                    providerPendingTransactions.Add((accountId, pendingTransaction));
                }

            }
        }

        private async Task CollateAccountTransactionsAsync(ExternalOpenBankingAccountTransactionsResponse? transactionsResults, string accountId, ConcurrentBag<(string, ExternalOpenBankingAccountTransaction)> providerTransactions)
        {
            if (transactionsResults == null)
            {
                return;
            }
            else if (transactionsResults.Results != null)
            {
                await foreach (var transaction in transactionsResults.Results)
                {
                    providerTransactions.Add((accountId, transaction));
                }
            }
        }

        private async Task CollateAccountBalanceAsync(ExternalOpenBankingGetAccountBalanceResponse? balanceResults, string accountId, ConcurrentBag<(string, ExternalOpenBankingAccountBalance)> providerBalances)
        {
            if (balanceResults == null)
            {
                return;
            }
            else if (balanceResults.Results != null)
            {
                await foreach (var accountBalance in balanceResults.Results)
                {
                    providerBalances.Add((accountId, accountBalance));
                }
            }
        }

        private async Task CollateAccountDirectDebitsAsync(ExternalOpenBankingAccountDirectDebitsResponse? directDebitsResults, string accountId, ConcurrentBag<(string AccountId, ExternalOpenBankingDirectDebit DirectDebits)> providerDirectDebits)
        {
            if (directDebitsResults == null)
            {
                return;
            }
            else if (directDebitsResults.Results != null)
            {
                await foreach (var directDebit in directDebitsResults.Results)
                {
                    providerDirectDebits.Add((accountId, directDebit));
                }
            }
        }

        private async Task CollateAccountStandingOrdersAsync(ExternalOpenBankingAccountStandingOrdersResponse? standingOrdersResults, string accountId, ConcurrentBag<(string AccountId, ExternalOpenBankingAccountStandingOrder StandingOrders)> providerStandingOrders)
        {
            if (standingOrdersResults == null)
            {
                return;
            }
            else if(standingOrdersResults.Results != null)
            {
                await foreach (var standingOrder in standingOrdersResults.Results)
                {
                    providerStandingOrders.Add((accountId, standingOrder));
                }

            }
        }

        private async Task<ExternalOpenBankingAccountStandingOrdersResponse?> GetOpenBankingStandingOrdersForAccountAsync(OpenBankingProvider provider, SyncTypes syncFlags, ExternalOpenBankingAccount account, string authToken, ConcurrentBag<OpenBankingSynchronization> performedSyncs, IEnumerable<OpenBankingSynchronization> relevantSyncs, CancellationToken cancellationToken)
        {
            if (ShouldSynchronise(syncFlags, relevantSyncs, SyncTypes.StandingOrders))
            {
                var standingOrders = await _openBankingApiService.GetAccountStandingOrdersAsync(account.AccountId, authToken, cancellationToken);
                performedSyncs.Add(CreateSyncLog(provider, account.AccountId, SyncTypes.StandingOrders));
                return standingOrders;
            }

            return null;
        }

        private async Task<ExternalOpenBankingAccountDirectDebitsResponse?> GetOpenBankingDirectDebitsForAccountAsync(OpenBankingProvider provider, SyncTypes syncFlags, ExternalOpenBankingAccount account, string authToken, ConcurrentBag<OpenBankingSynchronization> performedSyncs, IEnumerable<OpenBankingSynchronization> relevantSyncs, CancellationToken cancellationToken)
        {
            if (ShouldSynchronise(syncFlags, relevantSyncs, SyncTypes.DirectDebits))
            {
                var directDebits = await _openBankingApiService.GetAccountDirectDebitsAsync(account.AccountId, authToken, cancellationToken);
                performedSyncs.Add(CreateSyncLog(provider, account.AccountId, SyncTypes.DirectDebits));
                return directDebits;
            }


            return null;
        }

        private async Task<ExternalOpenBankingGetAccountBalanceResponse?> GetOpenBankingAccountBalanceAsync(OpenBankingProvider provider, SyncTypes syncFlags, ExternalOpenBankingAccount account, string authToken, ConcurrentBag<OpenBankingSynchronization> performedSyncs, IEnumerable<OpenBankingSynchronization> relevantSyncs, CancellationToken cancellationToken)
        {
            if (ShouldSynchronise(syncFlags, relevantSyncs, SyncTypes.Balance))
            {
                var balance = await _openBankingApiService.GetAccountBalanceAsync(account.AccountId, authToken, cancellationToken);
                performedSyncs.Add(CreateSyncLog(provider, account.AccountId, SyncTypes.Balance));
                return balance;
            }

            return null;
        }

        private async Task<ExternalOpenBankingAccountTransactionsResponse?> GetOpenBankingTransactionsForAccountAsync(OpenBankingProvider provider, SyncTypes syncFlags, ExternalOpenBankingAccount account, string authToken, ConcurrentBag<OpenBankingSynchronization> performedSyncs, IEnumerable<OpenBankingSynchronization> relevantSyncs, OpenBankingTransaction? latestTransactionForAccount, CancellationToken cancellationToken)
        {
            if (ShouldSynchronise(syncFlags, relevantSyncs, SyncTypes.Transactions))
            {
                var transactions = await _openBankingApiService.GetAccountTransactionsAsync(account.AccountId, authToken, latestTransactionForAccount?.TransactionTime, cancellationToken);
                performedSyncs.Add(CreateSyncLog(provider, account.AccountId, SyncTypes.Transactions));
                return transactions;
            }
            return null;
        }

        private async Task<ExternalOpenBankingAccountTransactionsResponse?> GetOpenBankingPendingTransactionsForAccountAsync(OpenBankingProvider provider, SyncTypes syncFlags, ExternalOpenBankingAccount account, string authToken, ConcurrentBag<OpenBankingSynchronization> performedSyncs, IEnumerable<OpenBankingSynchronization> relevantSyncs, OpenBankingTransaction? latestTransactionForAccount, CancellationToken cancellationToken)
        {
            if (ShouldSynchronise(syncFlags, relevantSyncs, SyncTypes.Transactions))
            {
                var transactions = await _openBankingApiService.GetAccountPendingTransactionsAsync(account.AccountId, authToken, latestTransactionForAccount?.TransactionTime, cancellationToken);
                performedSyncs.Add(CreateSyncLog(provider, account.AccountId, SyncTypes.PendingTransactions));
                return transactions;
            }
            return null;
        }

        private OpenBankingSynchronization CreateSyncLog(OpenBankingProvider provider, string accountId, SyncTypes syncType)
        {
            return new OpenBankingSynchronization()
            {
                Created = DateTime.Now.ToUniversalTime(),
                SyncronisationTime = DateTime.Now.ToUniversalTime(),
                SyncronisationType = (int)syncType,
                ProviderId = provider.Id,
                Provider = provider,
                OpenBankingAccountId = accountId,
                Id = Guid.NewGuid()
            };
        }

        private static bool ShouldSynchronise(SyncTypes syncFlags, IEnumerable<OpenBankingSynchronization> relevantSyncs, SyncTypes typeToCheck)
        {
            return (syncFlags.HasFlag(SyncTypes.All) || syncFlags.HasFlag(typeToCheck)) && !relevantSyncs.Any(x => x.SyncronisationType == (int)typeToCheck);
        }

        private async Task<string> GetAccessTokenAsync(OpenBankingProvider provider, CancellationToken cancellationToken)
        {
            await using var context = await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
                var accessToken = await context.IsolateToUser(UserId)
                    .Include(x => x.OpenBankingAccessTokens)
                    .AsNoTracking()
                    .SelectMany(x => x.OpenBankingAccessTokens)
                    .Where(x => x.ProviderId == provider.Id)
                    .OrderByDescending(x => x.Created)
                    .FirstOrDefaultAsync(cancellationToken: cancellationToken);

                if (accessToken.Created.AddSeconds(accessToken.ExpiresIn) > DateTime.Now.ToUniversalTime())
                {
                    return accessToken.AccessToken;
                }
                else
                {
                    var refreshTokenResponse = await _openBankingApiService.GetAccessTokenByRefreshTokenAsync(accessToken.RefreshToken, cancellationToken);

                    var newAccessToken = new OpenBankingAccessToken()
                    {
                        AccessToken = refreshTokenResponse.AccessToken,
                        ExpiresIn = refreshTokenResponse.ExpiresIn,
                        ProviderId = provider.Id,
                        RefreshToken = refreshTokenResponse.RefreshToken,
                        Created = DateTime.Now.ToUniversalTime()
                    };

                    await context.AddAsync(newAccessToken, cancellationToken);
                    await context.SaveChangesAsync(cancellationToken);

                    return newAccessToken.AccessToken;
                }
        }

        private async Task EnsureAuthenticatedAsync(OpenBankingProvider provider, CancellationToken cancellationToken)
        {
            await using var context = await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
            // User hasn't authenticated yet
            if (await context.OpenBankingAccessTokens
                .AsNoTracking()
                .Where(x => x.ProviderId == provider.Id)
                .CountAsync(cancellationToken: cancellationToken) == 0)
            {
                var response = await _openBankingApiService.ExchangeCodeForAccessTokenAsync(provider.AccessCode, cancellationToken);

                var accessToken = new OpenBankingAccessToken()
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

        private OpenBankingAccountBalance UpdateOrCreateAccountBalance(
            ExternalOpenBankingAccountBalance balance, OpenBankingAccount account)
        {
            var trackedBalance = account.AccountBalance;

            if (trackedBalance is not null)
            {
                trackedBalance.Current = balance.Current;
                trackedBalance.Available = balance.Available;
                trackedBalance.Updated = DateTime.Now.ToUniversalTime();
            }
            else
            {
                var newAccountBalance = new OpenBankingAccountBalance()
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

        private async Task<OpenBankingTransaction> UpdateOrCreateAccountTransaction(ExternalOpenBankingAccountTransaction transaction, OpenBankingAccount account, bool isPendingTransaction, OpenBankingProvider provider)
        {
            account.Transactions ??= [];
            provider.Transactions ??= [];
            
            var trackedTransaction = account.Transactions.FirstOrDefault(x => x.TransactionId == transaction.TransactionId);

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
                var newTransaction = new OpenBankingTransaction()
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

                await foreach (var classification in transaction.TransactionClassification)
                {
                    newTransaction.Classifications.Add(new OpenBankingTransactionClassifications() { Classification = classification, TransactionId = newTransaction.Id, Id = Guid.NewGuid() });
                }

                return newTransaction;
            }
            return trackedTransaction;
        }

        private OpenBankingAccount UpdateOrCreateAccount(ExternalOpenBankingAccount account, OpenBankingProvider provider)
        {
            var trackedAccount = provider.Accounts.FirstOrDefault(x => x.OpenBankingAccountId == account.AccountId);

            if (trackedAccount is not null)
            {
                trackedAccount.Updated = DateTime.Now.ToUniversalTime();
            }
            else
            {
                var newAccount = new OpenBankingAccount()
                {
                    OpenBankingAccountId = account.AccountId,
                    Id = Guid.NewGuid(),
                    AccountType = account.AccountType,
                    Currency = account.Currency,
                    DisplayName = account.DisplayName,
                    Created = DateTime.Now.ToUniversalTime(),
                    ProviderId = provider.Id
                };
                provider.Accounts.Add(newAccount);
                
                return newAccount;
            }
            return trackedAccount;
        }

        private OpenBankingStandingOrder UpdateOrCreateAccountStandingOrder(ExternalOpenBankingAccountStandingOrder standingOrder, OpenBankingAccount account)
        {
            account.StandingOrders ??= [];
            
            var trackedStandingOrder = account.StandingOrders.FirstOrDefault(x => x.Reference == standingOrder.Reference && x.Payee == standingOrder.Payee);

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
                var newStandingOrder = new OpenBankingStandingOrder()
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
        private OpenBankingDirectDebit UpdateOrCreateAccountDirectDebit(ExternalOpenBankingDirectDebit directDebit, OpenBankingAccount account)
        {
            account.DirectDebits ??= [];
            var trackedDirectDebit =  account.DirectDebits.FirstOrDefault(x => x.OpenBankingDirectDebitId == directDebit.DirectDebitId);

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
                var newDirectDebit = new OpenBankingDirectDebit()
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