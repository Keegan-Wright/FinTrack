using System.Runtime.CompilerServices;
using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.OpenBanking.Transactions;
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

    public TransactionsService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        IOpenBankingService openBankingService,
        ILogger<TransactionsService> logger) : base(currentUser, financeTrackerContextFactory, logger) =>
        _openBankingService = openBankingService;

    public async IAsyncEnumerable<Transaction> GetAllTransactionsAsync(
        FilterTransactions filterTransactions, SyncTypes syncTypes,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        TransactionCriteria criteria = TransactionCriteria.From(filterTransactions);
        Dictionary<Guid, string> customTags = await context.CustomClassifications.OwnedBy(UserId)
            .ToDictionaryAsync(static x => x.Id, static x => x.Tag, cancellationToken);

        List<BankTransaction> transactions = await criteria
            .ApplyServerFilters(context.BankTransactions.OwnedBy(UserId), context)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Encrypted timestamps sort by ciphertext in SQL, order after decryption
        foreach (BankTransaction transaction in transactions
                     .Where(x => criteria.MatchesClientFilters(x, customTags))
                     .OrderByDescending(static x => x.OccurredAt))
        {
            yield return new Transaction
            {
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                Description = transaction.Description,
                Pending = transaction.IsPending,
                TransactionCategory = transaction.Category,
                TransactionId = transaction.Id,
                TransactionTime = transaction.OccurredAt,
                TransactionType = transaction.Type.ToString(),
                Tags = TransactionCriteria.GetTags(transaction, customTags).ToList()
            };
        }
    }

    public async IAsyncEnumerable<TransactionAccountFilter> GetAccountsForTransactionFiltersAsync(
        SyncTypes syncTypes, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        IAsyncEnumerable<TransactionAccountFilter> accounts = context.BankAccounts.OwnedBy(UserId)
            .AsNoTracking()
            .Select(static x => new TransactionAccountFilter { AccountId = x.Id, AccountName = x.DisplayName })
            .AsAsyncEnumerable();

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

        IAsyncEnumerable<TransactionProviderFilter> providers = context.BankConnections.OwnedBy(UserId)
            .AsNoTracking()
            .Select(static x => new TransactionProviderFilter { ProviderId = x.Id, ProviderName = x.Name })
            .AsAsyncEnumerable();

        await foreach (TransactionProviderFilter provider in providers.WithCancellation(cancellationToken))
        {
            yield return provider;
        }
    }

    public async IAsyncEnumerable<TransactionTypeFilter> GetTypesForTransactionFiltersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Encrypted, so equal types only compare equal once decrypted
        List<TransactionType> types = await context.BankTransactions.OwnedBy(UserId)
            .Select(static x => x.Type)
            .ToListAsync(cancellationToken);

        foreach (TransactionType type in types.Distinct())
        {
            yield return new TransactionTypeFilter { TransactionType = type.ToString() };
        }
    }

    public async IAsyncEnumerable<TransactionCategoryFilter> GetCategoriesForTransactionFiltersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Encrypted, so equal categories only compare equal once decrypted
        List<string> categories = await context.BankTransactions.OwnedBy(UserId)
            .Select(static x => x.Category)
            .ToListAsync(cancellationToken);

        foreach (string category in categories.Distinct())
        {
            yield return new TransactionCategoryFilter { TransactionCategory = category };
        }
    }

    public async IAsyncEnumerable<TransactionTagFilter> GetTagsForTransactionFiltersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Unable to use .SelectMany on the encrypted column ProviderClassifications
        List<IReadOnlyCollection<string>> materialisedClassifications = await context.BankTransactions
            .OwnedBy(UserId)
            .Select(static x => x.ProviderClassifications)
            .ToListAsync(cancellationToken);

        List<string> providerTags = materialisedClassifications
            .SelectMany(static x => x)
            .Distinct()
            .ToList();

        List<string> customTags = await context.CustomClassifications
            .OwnedBy(UserId)
            .Select(static x => x.Tag)
            .ToListAsync(cancellationToken);

        foreach (string tag in providerTags.Union(customTags))
        {
            yield return new TransactionTagFilter { Tag = tag };
        }
    }
}
