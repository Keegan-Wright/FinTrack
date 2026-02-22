using System.Runtime.CompilerServices;
using System.Security.Claims;
using FinanceTracker.Data;
using FinanceTracker.Data.Models;
using FinanceTracker.Enums;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Models.Response.Account;
using FinanceTracker.Services.OpenBanking;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Services.Account;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<IAccountService>]
public class AccountService : ServiceBase, IAccountService
{
    private readonly IOpenBankingService _openBankingService;

    public AccountService(ClaimsPrincipal user, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        IOpenBankingService openBankingService) : base(user, financeTrackerContextFactory) =>
        _openBankingService = openBankingService;

    public async IAsyncEnumerable<AccountAndTransactionsResponse> GetAccountsAndMostRecentTransactionsAsync(
        int transactionsToReturn, SyncTypes syncFlags,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await _openBankingService.PerformSyncAsync(syncFlags, cancellationToken);
        await using FinanceTrackerContext context =
            await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<OpenBankingAccount> query = context
            .IsolateToUser(UserId)
            .Include(x => x.Providers).ThenInclude(x => x.Accounts).ThenInclude(x => x.Provider)
            .Include(x => x.Providers).ThenInclude(a => a.Accounts).ThenInclude(x => x.AccountBalance)
            .Include(x => x.Providers).ThenInclude(x => x.Accounts).ThenInclude(x =>
                x.Transactions.OrderByDescending(c => c.TransactionTime).Take(transactionsToReturn))
            .SelectMany(x => x.Providers.SelectMany(c => c.Accounts))
            .AsNoTracking();

        await foreach (OpenBankingAccount account in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            AccountAndTransactionsResponse response = new()
            {
                AccountBalance = account.AccountBalance?.Current ?? 0,
                AccountName = account.DisplayName,
                AccountType = account.AccountType,
                AvailableBalance = account.AccountBalance?.Available ?? 0,
                Logo = account.Provider.Logo,
                Transactions = account.Transactions?.Select(transaction => new AccountTransactionResponse
                {
                    Amount = transaction.Amount,
                    Description = transaction.Description,
                    Status = transaction.Pending ? "Pending" : "Complete",
                    Time = transaction.TransactionTime
                })
            };

            yield return response;
        }
    }
}
