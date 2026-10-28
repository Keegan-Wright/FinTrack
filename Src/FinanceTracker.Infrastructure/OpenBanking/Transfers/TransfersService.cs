using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Transaction = FinanceTracker.Contracts.Transactions.Transaction;

namespace FinanceTracker.Infrastructure.OpenBanking.Transfers;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ITransfersService>]
public class TransfersService : ServiceBase<TransfersService>, ITransfersService
{
    private readonly ITransferMatchingService _transferMatchingService;
    private readonly TimeProvider _timeProvider;

    public TransfersService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<TransfersService> logger,
        ITransferMatchingService transferMatchingService, TimeProvider timeProvider)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _transferMatchingService = transferMatchingService;
        _timeProvider = timeProvider;
    }

    public async IAsyncEnumerable<Transaction> GetTransfersToReviewAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Decisions are not encrypted since they contain no identifiable information
        List<BankTransaction> toReview = (await context.BankTransactions.OwnedBy(UserId).AsNoTracking()
                .Where(static x => x.Transfer != null && x.Transfer.Decision == TransferDecision.NeedsReview)
                .ToListAsync(cancellationToken))
            .OrderByDescending(static x => x.OccurredAt)
            .ToList();

        TransactionMapper mapper = await LoadMapperAsync(context, toReview, cancellationToken);

        foreach (BankTransaction transaction in toReview)
        {
            yield return mapper.ToContract(transaction);
        }
    }

    public async Task<int> GetReviewCountAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        return await context.BankTransactions.OwnedBy(UserId)
            .CountAsync(static x => x.Transfer != null && x.Transfer.Decision == TransferDecision.NeedsReview,
                cancellationToken);
    }

    public Task<IReadOnlyList<Transaction>> MarkAsTransferAsync(Guid id, CancellationToken cancellationToken) =>
        DecideAsync(id, (transaction, partner, decidedAt) =>
        {
            transaction.MarkAsTransfer(decidedAt);
            // Confirming a suggested pair confirms both halves, otherwise the other half would wait for review alone
            partner?.MarkAsTransfer(decidedAt);
        }, cancellationToken);

    public Task<IReadOnlyList<Transaction>> MarkNotATransferAsync(Guid id, CancellationToken cancellationToken) =>
        DecideAsync(id, (transaction, partner, decidedAt) =>
        {
            transaction.MarkNotATransfer(decidedAt);
            // The other half was only a transfer because of this one, so FinTrack decides about it again
            partner?.ClearTransfer(DecisionSource.User);
        }, cancellationToken);

    public Task<IReadOnlyList<Transaction>> ResetTransferAsync(Guid id, CancellationToken cancellationToken) =>
        DecideAsync(id, static (transaction, partner, _) =>
        {
            transaction.ClearTransfer(DecisionSource.User);
            // Halves confirmed together are reset together, a confirmed half would otherwise hold the other in place
            if (partner?.Transfer is { Source: DecisionSource.User })
            {
                partner.ClearTransfer(DecisionSource.User);
            }
        }, cancellationToken);

    /// <summary>
    /// Applies the user's <paramref name="decide"/> to the transaction and the other half that names it back, then
    /// re-matches around them so the pairs they affect update.
    /// </summary>
    /// <returns>The transaction and its other halves before and after, as they are now.</returns>
    private async Task<IReadOnlyList<Transaction>> DecideAsync(Guid id,
        Action<BankTransaction, BankTransaction?, DateTime> decide, CancellationToken cancellationToken)
    {
        HashSet<Guid> affected = [id];

        await using (FinanceTrackerContext context =
                     await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken))
        {
            BankTransaction transaction = await context.BankTransactions.OwnedBy(UserId)
                                              .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
                                          ?? throw new InvalidOperationException($"Transaction {id} was not found.");

            BankTransaction? partner = transaction.Transfer?.PartnerTransactionId is { } partnerId
                ? await context.BankTransactions.OwnedBy(UserId)
                    .SingleOrDefaultAsync(x => x.Id == partnerId, cancellationToken)
                : null;

            // Only a partner that names this one back is half of the same pair
            if (partner?.Transfer?.PartnerTransactionId != id)
            {
                partner = null;
            }

            if (transaction.Transfer?.PartnerTransactionId is { } before)
            {
                affected.Add(before);
            }

            decide(transaction, partner, _timeProvider.GetUtcNow().UtcDateTime);
            await context.SaveChangesAsync(cancellationToken);
        }

        await _transferMatchingService.MatchAroundAsync(affected, cancellationToken);

        return await LoadAffectedAsync(affected, cancellationToken);
    }

    /// <summary>
    /// <paramref name="ids"/> and whatever they're paired with now, mapped.
    /// </summary>
    private async Task<IReadOnlyList<Transaction>> LoadAffectedAsync(HashSet<Guid> ids,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        List<BankTransaction> transactions = await context.BankTransactions.OwnedBy(UserId).AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        List<Guid> partners = transactions
            .Select(static x => x.Transfer?.PartnerTransactionId)
            .OfType<Guid>()
            .Where(x => !ids.Contains(x))
            .ToList();

        if (partners.Count > 0)
        {
            transactions.AddRange(await context.BankTransactions.OwnedBy(UserId)
                .AsNoTracking()
                .Where(x => partners.Contains(x.Id))
                .ToListAsync(cancellationToken));
        }

        TransactionMapper mapper = await LoadMapperAsync(context, transactions, cancellationToken);
        return transactions.Select(mapper.ToContract).ToList();
    }

    private async Task<TransactionMapper> LoadMapperAsync(FinanceTrackerContext context,
        List<BankTransaction> transactions, CancellationToken cancellationToken)
    {
        Dictionary<Guid, string> customTags = await context.CustomClassifications.OwnedBy(UserId)
            .ToDictionaryAsync(static x => x.Id, static x => x.Tag, cancellationToken);

        return await TransactionMapper.LoadAsync(context, UserId, customTags, transactions, cancellationToken);
    }
}
