using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Account;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Account;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IAccountService>]
public class AccountService : ServiceBase<AccountService>, IAccountService
{
    private readonly IAccountScope _accountScope;

    public AccountService(ICurrentUser currentUser, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        ILogger<AccountService> logger, IAccountScope accountScope) : base(currentUser, financeTrackerContextFactory, logger)
    {
        _accountScope = accountScope;
    }

    public async IAsyncEnumerable<AccountAndTransactions> GetAccountsAndMostRecentTransactionsAsync(
        int transactionsToReturn,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        AccountsInView view = await _accountScope.ResolveAsync(context, cancellationToken);
        Dictionary<Guid, string> sharedBy = await view.SharedByAsync(context, cancellationToken);
        Dictionary<Guid, string> jointWith = await view.JointWithAsync(context, cancellationToken);

        List<BankAccount> accounts = await context.BankAccounts.InView(view)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // The connections behind the accounts in view, a shared account's being its owner's
        List<Guid> connectionIds = accounts.Select(static x => x.ConnectionId).Distinct().ToList();
        Dictionary<Guid, BankConnection> connections = await context.BankConnections
            .Where(x => connectionIds.Contains(x.Id))
            .AsNoTracking()
            .ToDictionaryAsync(static x => x.Id, cancellationToken);

        // Encrypted timestamps sort by ciphertext in SQL, so the most recent are picked after decryption. A hidden
        // copy's filling a joint account's gap are the shown copy's
        ILookup<Guid, BankTransaction> transactions = (await context.BankTransactions.InView(view)
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .ToLookup(x => view.ShownAs(x.AccountId));

        // A joint account whose shown copy has stopped syncing has the hidden copy's balance
        Dictionary<Guid, AccountBalance?> gapBalances =
            await JointAccountGaps.OpenGapBalancesAsync(context, view, cancellationToken);

        foreach (BankAccount account in accounts)
        {
            AccountBalance? balance = gapBalances.GetValueOrDefault(account.Id) ?? account.Balance;
            yield return new AccountAndTransactions
            {
                AccountId = account.Id,
                AccountBalance = balance?.Current ?? 0,
                AccountName = account.DisplayName,
                SharedBy = sharedBy.GetValueOrDefault(account.Id),
                JointWith = jointWith.GetValueOrDefault(account.Id),
                AccountType = account.AccountType,
                AvailableBalance = balance?.Available ?? 0,
                CardEnding = account.Card?.PartialNumber,
                CreditLimit = balance?.CreditLimit,
                ProviderName = connections[account.ConnectionId].Name,
                Logo = connections[account.ConnectionId].Logo,
                Transactions = transactions[account.Id]
                    .OrderByDescending(static x => x.OccurredAt)
                    .Take(transactionsToReturn)
                    .Select(transaction => new AccountTransaction
                    {
                        Amount = transaction.Amount,
                        Description = view.DescriptionOf(transaction),
                        Status = transaction.IsPending ? "Pending" : "Complete",
                        Time = transaction.OccurredAt
                    }).ToImmutableList()
            };
        }
    }
}
