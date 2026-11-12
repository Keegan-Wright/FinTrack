using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Transactions;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ITransactionsService>]
public class TransactionsService : ServiceBase<TransactionsService>, ITransactionsService
{
    private readonly TimeProvider _timeProvider;
    private readonly IAccountScope _accountScope;

    public TransactionsService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        ILogger<TransactionsService> logger, TimeProvider timeProvider, IAccountScope accountScope)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _accountScope = accountScope;
    }

    public async IAsyncEnumerable<Transaction> GetAllTransactionsAsync(
        FilterTransactions filterTransactions,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        AccountsInView view = await _accountScope.ResolveAsync(context, cancellationToken);
        TransactionCriteria criteria = await TransactionCriteria.From(filterTransactions)
            .WithSavingsGoalsAsync(context, UserId, _timeProvider.LocalTimeZone, cancellationToken);

        // A shared transaction's tags are its owner's, so theirs are named too. Only those on transactions in view
        // ever show
        Dictionary<Guid, string> customTags = await TagNames.LoadAsync(context, view, cancellationToken);

        List<BankTransaction> transactions = await criteria
            .ApplyServerFilters(context.BankTransactions.InView(view), context, view)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Encrypted timestamps sort by ciphertext in SQL, order after decryption
        List<BankTransaction> matching = transactions
            .Where(x => criteria.MatchesClientFilters(x, customTags, view))
            .OrderByDescending(static x => x.OccurredAt)
            .ToList();

        TransactionMapper mapper =
            await TransactionMapper.LoadAsync(context, view, customTags, matching,
                _timeProvider.LocalTimeZone, cancellationToken);

        foreach (BankTransaction transaction in matching)
        {
            yield return mapper.ToContract(transaction);
        }
    }

    public Task<Transaction> HideBankClassificationAsync(Guid transactionId, ChangeBankClassification request,
        CancellationToken cancellationToken) =>
        ChangeAsync(transactionId, "hid a bank classification on", (_, transaction) =>
        {
            if (!transaction.ProviderClassifications.Contains(request.Classification, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Transaction {transactionId} isn't classified as '{request.Classification}' by the bank.");
            }

            transaction.HideClassification(request.Classification);
            return Task.CompletedTask;
        }, cancellationToken);

    public Task<Transaction> ShowBankClassificationAsync(Guid transactionId, ChangeBankClassification request,
        CancellationToken cancellationToken)
    {
        return ChangeAsync(transactionId, "showed a bank classification on", (_, transaction) =>
        {
            transaction.ShowClassification(request.Classification);
            return Task.CompletedTask;
        }, cancellationToken);
    }

    public Task<Transaction> SplitAsync(Guid transactionId, SplitTransaction request,
        CancellationToken cancellationToken)
    {
        return ChangeAsync(transactionId, "split", async (context, transaction) =>
        {
            // Their own tags, or one already on it, which on a shared transaction may be the other person's
            List<Guid> onIt = [.. transaction.CustomClassificationIds, .. transaction.Splits.Select(static x => x.TagId)
                .OfType<Guid>()];
            List<Guid> tagIds = request.Parts.Select(static x => x.TagId).OfType<Guid>().Distinct().ToList();
            Guid userId = UserId;
            List<CustomClassification> tags = await context.CustomClassifications
                .Where(x => tagIds.Contains(x.Id) && (x.UserId == userId || onIt.Contains(x.Id)))
                .ToListAsync(cancellationToken);

            List<TransactionSplit> parts = request.Parts
                .Select(static x => new TransactionSplit(x.Amount, x.TagId, Trimmed(x.Note)))
                .ToList();

            try
            {
                transaction.Split(parts, tags, UserId);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                throw new CannotSplitTransactionException(exception.Message, exception);
            }

            foreach (CustomClassification tag in tags.Where(x => x.UserId == userId))
            {
                await AnnotationShares.ShareWithOwnerAsync(context, userId, transaction,
                    ShareItemType.Classification, tag.Id, _timeProvider.UtcNow, cancellationToken);
            }
        }, cancellationToken);
    }

    public Task<Transaction> UnsplitAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        return ChangeAsync(transactionId, "unsplit", (_, transaction) =>
        {
            transaction.Unsplit();
            return Task.CompletedTask;
        }, cancellationToken);
    }

    private static string? Trimmed(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        return note.Trim();
    }

    /// <summary>
    /// Makes <paramref name="change"/> to a transaction the user can change, their own or one on an account shared
    /// with changes allowed, and returns it as it is now.
    /// </summary>
    /// <param name="action">What the change was, for the log, e.g. <c>split</c>.</param>
    /// <exception cref="InvalidOperationException">The user can't change any such transaction.</exception>
    private async Task<Transaction> ChangeAsync(Guid transactionId, string action,
        Func<FinanceTrackerContext, BankTransaction, Task> change, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        AccountsInView changeable = await _accountScope.ResolveChangeableAsync(context, cancellationToken);
        BankTransaction transaction = await context.BankTransactions.ChangeableIn(changeable)
            .SingleAsync(x => x.Id == transactionId, cancellationToken);

        await change(context, transaction);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, action, "transaction", transactionId);

        AccountsInView view = await _accountScope.ResolveAllAsync(context, cancellationToken);
        TransactionMapper mapper = await TransactionMapper.LoadAsync(context, view, [transaction],
            _timeProvider.LocalTimeZone, cancellationToken);

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

        AccountsInView view = await _accountScope.ResolveAsync(context, cancellationToken);
        IAsyncEnumerable<TransactionAccountFilter> accounts = context.BankAccounts.InView(view)
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

        // Their own banks, and the banks behind accounts shared with them, a shared account's being its owner's
        AccountsInView view = await _accountScope.ResolveAsync(context, cancellationToken);
        List<Guid> sharedIds = view.SharedAccountIds.ToList();
        List<Guid> sharedConnectionIds = await context.BankAccounts
            .Where(x => sharedIds.Contains(x.Id))
            .Select(static x => x.ConnectionId)
            .Distinct()
            .ToListAsync(cancellationToken);
        Guid userId = UserId;
        IAsyncEnumerable<TransactionProviderFilter> providers = context.BankConnections
            .Where(x => x.UserId == userId || sharedConnectionIds.Contains(x.Id))
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
        AccountsInView view = await _accountScope.ResolveAsync(context, cancellationToken);
        List<TransactionType> types = await context.BankTransactions.InView(view)
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
        AccountsInView view = await _accountScope.ResolveAsync(context, cancellationToken);
        List<string> categories = await context.BankTransactions.InView(view)
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
        AccountsInView view = await _accountScope.ResolveAsync(context, cancellationToken);
        var materialisedClassifications = await context.BankTransactions
            .InView(view)
            .AsNoTracking()
            .Select(static x => new { x.ProviderClassifications, x.HiddenClassifications, x.CustomClassificationIds })
            .ToListAsync(cancellationToken);

        List<string> providerTags = materialisedClassifications
            .SelectMany(static x => VisibleClassifications(x.ProviderClassifications, x.HiddenClassifications))
            .Distinct()
            .ToList();

        // Their own tags, and anyone else's on a transaction in view, such as the other person's on a shared one
        HashSet<Guid> onTransactions = materialisedClassifications
            .SelectMany(static x => x.CustomClassificationIds)
            .ToHashSet();
        Dictionary<Guid, string> tagNames = await TagNames.LoadAsync(context, view, cancellationToken);
        Guid userId = UserId;
        List<Guid> ownTagIds = await context.CustomClassifications.OwnedBy(userId)
            .Select(static x => x.Id)
            .ToListAsync(cancellationToken);
        List<string> customTags = tagNames
            .Where(x => onTransactions.Contains(x.Key) || ownTagIds.Contains(x.Key))
            .Select(static x => x.Value)
            .Distinct()
            .ToList();

        foreach (string tag in providerTags.Union(customTags))
        {
            yield return new TransactionTagFilter { Tag = tag };
        }
    }
}
