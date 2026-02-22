using System.Runtime.CompilerServices;
using System.Security.Claims;
using FinanceTracker.Data;
using FinanceTracker.Data.Models;
using FinanceTracker.Enums;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Models.Request.Transaction;
using FinanceTracker.Models.Response.Transaction;
using FinanceTracker.Services.OpenBanking;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Services.Transactions;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<ITransactionsService>]
public class TransactionsService : ServiceBase, ITransactionsService
{
    private readonly IOpenBankingService _openBankingService;

    public TransactionsService(ClaimsPrincipal user,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        IOpenBankingService openBankingService) : base(user, financeTrackerContextFactory) =>
        _openBankingService = openBankingService;

    public async IAsyncEnumerable<TransactionResponse> GetAllTransactionsAsync(
        FilteredTransactionsRequest filteredTransactionsRequest, SyncTypes syncTypes,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await _openBankingService.PerformSyncAsync(syncTypes, cancellationToken);
        await using FinanceTrackerContext context =
            await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<OpenBankingTransaction> transactionsQuery = context.IsolateToUser(UserId)
            .Include(x => x.Providers).ThenInclude(x => x.Accounts).ThenInclude(x => x.Transactions)
            .ThenInclude(x => x.Classifications)
            .SelectMany(x => x.Providers.SelectMany(c => c.Accounts).SelectMany(r => r.Transactions))
            .AsQueryable();

        transactionsQuery = ApplyTransactionRequestFiltering(filteredTransactionsRequest, transactionsQuery);


        await foreach (TransactionResponse transaction in GetTransactionsSelect(transactionsQuery)
                           .OrderByDescending(x => x.TransactionTime)
                           .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            // Few Client filters due to encryption limiting ability
            if (filteredTransactionsRequest.SearchTerm is not null)
            {
                bool containsSearchTerm = transaction.Description.ToLower()
                    .Contains(filteredTransactionsRequest.SearchTerm.ToLower());
                if (!containsSearchTerm)
                {
                    continue;
                }
            }


            if (filteredTransactionsRequest.FromDate is not null)
            {
                bool containsFromDate = transaction.TransactionTime >= filteredTransactionsRequest.FromDate;
                if (!containsFromDate)
                {
                    continue;
                }
            }

            if (filteredTransactionsRequest.ToDate is not null)
            {
                bool containsFromDate = transaction.TransactionTime <= filteredTransactionsRequest.ToDate;
                if (!containsFromDate)
                {
                    continue;
                }
            }


            yield return transaction;
        }
    }

    public async IAsyncEnumerable<TransactionAccountFilterResponse> GetAccountsForTransactionFiltersAsync(
        SyncTypes syncTypes, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await _openBankingService.PerformSyncAsync(syncTypes, cancellationToken);

        await using FinanceTrackerContext context =
            await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<TransactionAccountFilterResponse> query = context.IsolateToUser(UserId).Include(x => x.Providers)
            .ThenInclude(x => x.Accounts)
            .SelectMany(x => x.Providers.SelectMany(c => c.Accounts))
            .AsNoTracking()
            .Select(x => new TransactionAccountFilterResponse { AccountId = x.Id, AccountName = x.DisplayName });

        IAsyncEnumerable<TransactionAccountFilterResponse> accounts = query.AsAsyncEnumerable();

        await foreach (TransactionAccountFilterResponse account in accounts.WithCancellation(cancellationToken))
        {
            yield return account;
        }
    }

    public async IAsyncEnumerable<TransactionProviderFilterResponse> GetProvidersForTransactionFiltersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<TransactionProviderFilterResponse> query = context.IsolateToUser(UserId)
            .Include(x => x.Providers).ThenInclude(x => x.Accounts)
            .SelectMany(x => x.Providers)
            .Select(x => new TransactionProviderFilterResponse { ProviderId = x.Id, ProviderName = x.Name }).Distinct();

        await foreach (TransactionProviderFilterResponse provider in query.AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
        {
            yield return provider;
        }
    }

    public async IAsyncEnumerable<TransactionTypeFilterResponse> GetTypesForTransactionFiltersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<TransactionTypeFilterResponse> query = context.IsolateToUser(UserId)
            .Include(x => x.Providers).ThenInclude(x => x.Accounts).ThenInclude(x => x.Transactions)
            .SelectMany(x => x.Providers.SelectMany(x => x.Accounts).SelectMany(c => c.Transactions))
            .Select(x => new TransactionTypeFilterResponse { TransactionType = x.TransactionType })
            .Distinct();

        await foreach (TransactionTypeFilterResponse type in query.AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
        {
            yield return type;
        }
    }

    public async IAsyncEnumerable<TransactionCategoryFilterResponse> GetCategoriesForTransactionFiltersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<string> query = context.IsolateToUser(UserId)
            .Include(x => x.Providers).ThenInclude(x => x.Accounts).ThenInclude(x => x.Transactions)
            .SelectMany(x => x.Providers.SelectMany(c => c.Accounts).SelectMany(r => r.Transactions))
            .Select(x => x.TransactionCategory).Distinct();

        await foreach (string category in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return new TransactionCategoryFilterResponse { TransactionCategory = category };
        }
    }

    public async IAsyncEnumerable<TransactionTagFilterResponse> GetTagsForTransactionFiltersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<TransactionTagFilterResponse> query = context.IsolateToUser(UserId)
            .Include(x => x.Providers).ThenInclude(x => x.Accounts).ThenInclude(x => x.Transactions)
            .ThenInclude(x => x.Classifications)
            .SelectMany(x =>
                x.Providers.SelectMany(c => c.Accounts).SelectMany(r => r.Transactions)
                    .SelectMany(t => t.Classifications))
            .Select(x => new TransactionTagFilterResponse { Tag = x.Classification })
            .Distinct();

        await foreach (TransactionTagFilterResponse type in query.AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
        {
            yield return type;
        }
    }


    private IQueryable<OpenBankingTransaction> ApplyTransactionRequestFiltering(
        FilteredTransactionsRequest filteredTransactionsRequest, IQueryable<OpenBankingTransaction> transactionsQuery)
    {
        if (filteredTransactionsRequest.AccountIds is not null && filteredTransactionsRequest.AccountIds.Any())
        {
            transactionsQuery =
                transactionsQuery.Where(x => filteredTransactionsRequest.AccountIds.Contains(x.Account.Id));
        }

        if (filteredTransactionsRequest.Types is not null && filteredTransactionsRequest.Types.Any())
        {
            transactionsQuery =
                transactionsQuery.Where(x => filteredTransactionsRequest.Types.Contains(x.TransactionType));
        }

        if (filteredTransactionsRequest.Categories is not null && filteredTransactionsRequest.Categories.Any())
        {
            transactionsQuery =
                transactionsQuery.Where(x => filteredTransactionsRequest.Categories.Contains(x.TransactionCategory));
        }

        if (filteredTransactionsRequest.Tags is not null && filteredTransactionsRequest.Tags.Any())
        {
            transactionsQuery = transactionsQuery.Where(x =>
                filteredTransactionsRequest.Tags.Any(y => x.Classifications.Any(c => c.Classification == y)));
        }

        if (filteredTransactionsRequest.ProviderIds is not null && filteredTransactionsRequest.ProviderIds.Any())
        {
            transactionsQuery = transactionsQuery
                .Where(x => filteredTransactionsRequest.ProviderIds.Contains(x.Provider.Id))
                .Select(x => x);
        }

        return transactionsQuery;
    }

    private IQueryable<TransactionResponse> GetTransactionsSelect(IQueryable<OpenBankingTransaction> query) =>
        query.Select(transaction => new TransactionResponse
        {
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Description = transaction.Description,
            Pending = transaction.Pending,
            TransactionCategory = transaction.TransactionCategory,
            TransactionId = transaction.Id,
            TransactionTime = transaction.TransactionTime,
            TransactionType = transaction.TransactionType,
            Tags = transaction.Classifications.Select(x => x.Classification)
        });
}
