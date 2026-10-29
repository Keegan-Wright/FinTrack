using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Account;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Account;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IAccountService>]
public class AccountService : ServiceBase<AccountService>, IAccountService
{
    public AccountService(ICurrentUser currentUser, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        ILogger<AccountService> logger) : base(currentUser, financeTrackerContextFactory, logger)
    {
    }

    public async IAsyncEnumerable<AccountAndTransactions> GetAccountsAndMostRecentTransactionsAsync(
        int transactionsToReturn,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        List<BankAccount> accounts = await context.BankAccounts.OwnedBy(UserId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        Dictionary<Guid, byte[]> logos = await context.BankConnections.OwnedBy(UserId)
            .ToDictionaryAsync(static x => x.Id, static x => x.Logo, cancellationToken);

        // Encrypted timestamps sort by ciphertext in SQL, so the most recent are picked after decryption
        ILookup<Guid, BankTransaction> transactions = (await context.BankTransactions.OwnedBy(UserId)
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .ToLookup(static x => x.AccountId);

        foreach (BankAccount account in accounts)
        {
            yield return new AccountAndTransactions
            {
                AccountBalance = account.Balance?.Current ?? 0,
                AccountName = account.DisplayName,
                AccountType = account.AccountType,
                AvailableBalance = account.Balance?.Available ?? 0,
                CardEnding = account.Card?.PartialNumber,
                CreditLimit = account.Balance?.CreditLimit,
                Logo = logos[account.ConnectionId],
                Transactions = transactions[account.Id]
                    .OrderByDescending(static x => x.OccurredAt)
                    .Take(transactionsToReturn)
                    .Select(static transaction => new AccountTransaction
                    {
                        Amount = transaction.Amount,
                        Description = transaction.Description,
                        Status = transaction.IsPending ? "Pending" : "Complete",
                        Time = transaction.OccurredAt
                    }).ToImmutableList()
            };
        }
    }
}
