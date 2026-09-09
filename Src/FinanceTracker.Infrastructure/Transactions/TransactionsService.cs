using System.Runtime.CompilerServices;
using System.Security.Claims;
using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.OpenBanking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Transactions;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<ITransactionsService>]
public class TransactionsService : ServiceBase<TransactionsService>, ITransactionsService
{
    private readonly IOpenBankingService _openBankingService;

    public TransactionsService(ClaimsPrincipal user,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        IOpenBankingService openBankingService,
        ILogger<TransactionsService> logger) : base(user, financeTrackerContextFactory, logger) =>
        _openBankingService = openBankingService;

    public async IAsyncEnumerable<Transaction> GetAllTransactionsAsync(
        FilterTransactions filterTransactions, SyncTypes syncTypes,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<OpenBankingTransaction> transactionsQuery = context.IsolateToUser(UserId)
            .Include(x => x.Providers)!.ThenInclude(x => x.Accounts)!.ThenInclude(x => x.Transactions)!
            .ThenInclude(x => x.Classifications)
            .SelectMany(x => x.Providers!.SelectMany(c => c.Accounts!).SelectMany(r => r.Transactions!))
            .AsQueryable();

        transactionsQuery = ApplyTransactionRequestFiltering(filterTransactions, transactionsQuery);

        var transactions = await GetTransactionsSelect(transactionsQuery).ToListAsync(cancellationToken);

        await foreach (Transaction transaction in transactions
                           .OrderByDescending(static x => x.TransactionTime)
                           .ToAsyncEnumerable().WithCancellation(cancellationToken))
        {
            // Few Client filters due to encryption limiting ability
            if (filterTransactions.SearchTerm is not null)
            {
                bool containsSearchTerm = transaction.Description.Contains(filterTransactions.SearchTerm, StringComparison.CurrentCultureIgnoreCase);
                if (!containsSearchTerm)
                {
                    continue;
                }
            }


            if (filterTransactions.FromDate is not null)
            {
                bool containsFromDate = transaction.TransactionTime >= filterTransactions.FromDate;
                if (!containsFromDate)
                {
                    continue;
                }
            }

            if (filterTransactions.ToDate is not null)
            {
                bool containsFromDate = transaction.TransactionTime <= filterTransactions.ToDate;
                if (!containsFromDate)
                {
                    continue;
                }
            }


            yield return transaction;
        }
    }

    public async IAsyncEnumerable<TransactionAccountFilter> GetAccountsForTransactionFiltersAsync(
        SyncTypes syncTypes, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<TransactionAccountFilter> query = context.IsolateToUser(UserId).Include(x => x.Providers)!
            .ThenInclude(x => x.Accounts)
            .SelectMany(x => x.Providers!.SelectMany(c => c.Accounts!))
            .AsNoTracking()
            .Select(x => new TransactionAccountFilter { AccountId = x.Id, AccountName = x.DisplayName });

        IAsyncEnumerable<TransactionAccountFilter> accounts = query.AsAsyncEnumerable();

        await foreach (TransactionAccountFilter account in accounts.WithCancellation(cancellationToken))
        {
            yield return account;
        }
    }

    public async IAsyncEnumerable<TransactionProviderFilter> GetProvidersForTransactionFiltersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<TransactionProviderFilter> query = context.IsolateToUser(UserId)
            .Include(x => x.Providers)!.ThenInclude(x => x.Accounts)
            .SelectMany(x => x.Providers!)
            .Select(x => new TransactionProviderFilter { ProviderId = x.Id, ProviderName = x.Name }).Distinct();

        await foreach (TransactionProviderFilter provider in query.AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
        {
            yield return provider;
        }
    }

    public async IAsyncEnumerable<TransactionTypeFilter> GetTypesForTransactionFiltersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<TransactionTypeFilter> query = context.IsolateToUser(UserId)
            .Include(x => x.Providers)!.ThenInclude(x => x.Accounts)!.ThenInclude(x => x.Transactions)
            .SelectMany(x => x.Providers!.SelectMany(c => c.Accounts!).SelectMany(c => c.Transactions!))
            .Select(x => new TransactionTypeFilter { TransactionType = x.TransactionType })
            .Distinct();

        await foreach (TransactionTypeFilter type in query.AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
        {
            yield return type;
        }
    }

    public async IAsyncEnumerable<TransactionCategoryFilter> GetCategoriesForTransactionFiltersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<string> query = context.IsolateToUser(UserId)
            .Include(x => x.Providers)!.ThenInclude(x => x.Accounts)!.ThenInclude(x => x.Transactions)
            .SelectMany(x => x.Providers!.SelectMany(c => c.Accounts!).SelectMany(r => r.Transactions!))
            .Select(x => x.TransactionCategory).Distinct();

        await foreach (string category in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return new TransactionCategoryFilter { TransactionCategory = category };
        }
    }

    public async IAsyncEnumerable<TransactionTagFilter> GetTagsForTransactionFiltersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<TransactionTagFilter> query = context.IsolateToUser(UserId)
            .Include(x => x.Providers)!.ThenInclude(x => x.Accounts)!.ThenInclude(x => x.Transactions)!
            .ThenInclude(x => x.Classifications)
            .SelectMany(x =>
                x.Providers!.SelectMany(c => c.Accounts!).SelectMany(r => r.Transactions!)
                    .SelectMany(t => t.Classifications!))
            .Select(x => new TransactionTagFilter { Tag = x.Classification })
            .Distinct();

        await foreach (TransactionTagFilter type in query.AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
        {
            yield return type;
        }
    }


    private static IQueryable<OpenBankingTransaction> ApplyTransactionRequestFiltering(
        FilterTransactions filterTransactions, IQueryable<OpenBankingTransaction> transactionsQuery)
    {
        if (filterTransactions.AccountIds is not null && filterTransactions.AccountIds.Any())
        {
            transactionsQuery =
                transactionsQuery.Where(x => filterTransactions.AccountIds.Contains(x.Account!.Id));
        }

        if (filterTransactions.Types is not null && filterTransactions.Types.Any())
        {
            transactionsQuery =
                transactionsQuery.Where(x => filterTransactions.Types.Contains(x.TransactionType));
        }

        if (filterTransactions.Categories is not null && filterTransactions.Categories.Any())
        {
            transactionsQuery =
                transactionsQuery.Where(x => filterTransactions.Categories.Contains(x.TransactionCategory));
        }

        if (filterTransactions.Tags is not null && filterTransactions.Tags.Any())
        {
            transactionsQuery = transactionsQuery.Where(x =>
                filterTransactions.Tags.Any(y => x.Classifications!.Any(c => c.Classification == y)));
        }

        if (filterTransactions.ProviderIds is not null && filterTransactions.ProviderIds.Any())
        {
            transactionsQuery = transactionsQuery
                .Where(x => filterTransactions.ProviderIds.Contains(x.Provider!.Id))
                .Select(x => x);
        }

        return transactionsQuery;
    }

    private static IQueryable<Transaction> GetTransactionsSelect(IQueryable<OpenBankingTransaction> query) =>
        query.Select(transaction => new Transaction
        {
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Description = transaction.Description,
            Pending = transaction.Pending,
            TransactionCategory = transaction.TransactionCategory,
            TransactionId = transaction.Id,
            TransactionTime = transaction.TransactionTime,
            TransactionType = transaction.TransactionType,
            Tags = transaction.Classifications!.Select(x => x.Classification)
        });
}
