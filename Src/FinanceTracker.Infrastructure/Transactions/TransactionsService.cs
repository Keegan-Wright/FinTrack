using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Transactions;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ITransactionsService>]
public class TransactionsService : ServiceBase<TransactionsService>, ITransactionsService
{
    public TransactionsService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        ILogger<TransactionsService> logger) : base(currentUser, financeTrackerContextFactory, logger)
    {
    }

    public async IAsyncEnumerable<Transaction> GetAllTransactionsAsync(
        FilterTransactions filterTransactions,
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
        List<BankTransaction> matching = transactions
            .Where(x => criteria.MatchesClientFilters(x, customTags))
            .OrderByDescending(static x => x.OccurredAt)
            .ToList();

        TransactionMapper mapper =
            await TransactionMapper.LoadAsync(context, UserId, customTags, matching, cancellationToken);

        foreach (BankTransaction transaction in matching)
        {
            yield return mapper.ToContract(transaction);
        }
    }

    public Task<Transaction> HideBankClassificationAsync(Guid transactionId, ChangeBankClassification request,
        CancellationToken cancellationToken) =>
        ChangeBankClassificationAsync(transactionId, transaction =>
        {
            if (!transaction.ProviderClassifications.Contains(request.Classification, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Transaction {transactionId} isn't classified as '{request.Classification}' by the bank.");
            }

            transaction.HideClassification(request.Classification);
        }, cancellationToken);

    public Task<Transaction> ShowBankClassificationAsync(Guid transactionId, ChangeBankClassification request,
        CancellationToken cancellationToken) =>
        ChangeBankClassificationAsync(transactionId,
            transaction => transaction.ShowClassification(request.Classification), cancellationToken);

    /// <summary>
    /// Makes <paramref name="change"/> to one of the user's transactions and returns it as it is now.
    /// </summary>
    private async Task<Transaction> ChangeBankClassificationAsync(Guid transactionId,
        Action<BankTransaction> change, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        BankTransaction transaction = await context.BankTransactions.OwnedBy(UserId)
            .SingleAsync(x => x.Id == transactionId, cancellationToken);

        change(transaction);
        await context.SaveChangesAsync(cancellationToken);

        Dictionary<Guid, string> customTags = await context.CustomClassifications.OwnedBy(UserId)
            .ToDictionaryAsync(static x => x.Id, static x => x.Tag, cancellationToken);
        TransactionMapper mapper =
            await TransactionMapper.LoadAsync(context, UserId, customTags, [transaction], cancellationToken);

        return mapper.ToContract(transaction);
    }

    /// <summary>
    /// The classifications not among <paramref name="hidden"/>. A transaction from before hiding existed has no hidden
    /// list at all, and projected on its own that comes back null rather than empty.
    /// </summary>
    private static IEnumerable<string> VisibleClassifications(IEnumerable<string> classifications,
        IReadOnlyList<HiddenClassification>? hidden)
    {
        if (hidden is not { Count: > 0 })
        {
            return classifications;
        }

        return classifications.Where(classification => !hidden.Any(x =>
            string.Equals(x.Classification, classification, StringComparison.OrdinalIgnoreCase)));
    }

    public async IAsyncEnumerable<TransactionAccountFilter> GetAccountsForTransactionFiltersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
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

        // Unable to use .SelectMany on the encrypted column ProviderClassifications. Hidden ones are left out unless
        // another transaction still shows them
        var materialisedClassifications = await context.BankTransactions
            .OwnedBy(UserId)
            .AsNoTracking()
            .Select(static x => new { x.ProviderClassifications, x.HiddenClassifications })
            .ToListAsync(cancellationToken);

        List<string> providerTags = materialisedClassifications
            .SelectMany(static x => VisibleClassifications(x.ProviderClassifications, x.HiddenClassifications))
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
