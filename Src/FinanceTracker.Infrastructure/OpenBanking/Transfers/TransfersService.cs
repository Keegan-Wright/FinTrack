using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Transaction = FinanceTracker.Contracts.Transactions.Transaction;

namespace FinanceTracker.Infrastructure.OpenBanking.Transfers;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ITransfersService>]
public class TransfersService : ServiceBase<TransfersService>, ITransfersService
{
    private readonly ITransferMatchingService _transferMatchingService;
    private readonly TimeProvider _timeProvider;
    private readonly IAccountScope _accountScope;
    private readonly IServiceScopeFactory _scopeFactory;

    public TransfersService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<TransfersService> logger,
        ITransferMatchingService transferMatchingService, TimeProvider timeProvider, IAccountScope accountScope,
        IServiceScopeFactory scopeFactory)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _transferMatchingService = transferMatchingService;
        _timeProvider = timeProvider;
        _accountScope = accountScope;
        _scopeFactory = scopeFactory;
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

        // Every account shared with them, so another person's half shows when it's on one they can see
        AccountsInView view = await _accountScope.ResolveAllAsync(context, cancellationToken);
        TransactionMapper mapper = await TransactionMapper.LoadAsync(context, view, toReview,
            _timeProvider.LocalTimeZone, cancellationToken);

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
        DecideAsync(id, "marked as a transfer", (transaction, partner, decidedAt) =>
        {
            transaction.MarkAsTransfer(decidedAt);
            // Confirming a suggested pair confirms both halves, otherwise the other half would wait for review alone
            partner?.MarkAsTransfer(decidedAt);
        }, cancellationToken);

    public Task<IReadOnlyList<Transaction>> MarkNotATransferAsync(Guid id, CancellationToken cancellationToken) =>
        DecideAsync(id, "marked as not a transfer", (transaction, partner, decidedAt) =>
        {
            transaction.MarkNotATransfer(decidedAt);
            // The other half was only a transfer because of this one, so FinTrack decides about it again
            partner?.ClearTransfer(DecisionSource.User);
        }, cancellationToken);

    public Task<IReadOnlyList<Transaction>> ResetTransferAsync(Guid id, CancellationToken cancellationToken) =>
        DecideAsync(id, "reset the transfer answer on", static (transaction, partner, _) =>
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
    /// re-matches around them so the pairs they affect update. The transaction is the user's own, or on an account
    /// shared with them with changes allowed.
    /// </summary>
    /// <param name="action">What the user decided, for the log, e.g. <c>marked as a transfer</c>.</param>
    /// <returns>The transaction and its other halves before and after, as they are now, those the user can see.
    /// </returns>
    private async Task<IReadOnlyList<Transaction>> DecideAsync(Guid id, string action,
        Action<BankTransaction, BankTransaction?, DateTime> decide, CancellationToken cancellationToken)
    {
        HashSet<Guid> affected = [id];
        Guid ownerId;

        await using (FinanceTrackerContext context =
                     await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken))
        {
            AccountsInView changeable = await _accountScope.ResolveChangeableAsync(context, cancellationToken);
            BankTransaction transaction = await context.BankTransactions.ChangeableIn(changeable)
                                              .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
                                          ?? throw new NotFoundException($"Transaction {id} was not found.");
            ownerId = transaction.UserId;

            // The other half may be on an account the user can't see, one the owner keeps private, or the other
            // person's across a link. It's half of the same pair, so it's answered with it, checked below
            BankTransaction? partner = transaction.Transfer?.PartnerTransactionId is { } partnerId
                ? await context.BankTransactions.SingleOrDefaultAsync(x => x.Id == partnerId, cancellationToken)
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

            decide(transaction, partner, _timeProvider.UtcNow);
            await context.SaveChangesAsync(cancellationToken);
            Logger.LogChanged(UserId, action, "transaction", id);
        }

        await MatchAroundAsOwnerAsync(ownerId, affected, cancellationToken);

        return await LoadAffectedAsync(affected, cancellationToken);
    }

    /// <summary>
    /// Re-matches around <paramref name="affected"/> as <paramref name="ownerId"/>, whose transactions they are, so
    /// it's among their accounts, the other person's included only on their side.
    /// </summary>
    private async Task MatchAroundAsOwnerAsync(Guid ownerId, HashSet<Guid> affected,
        CancellationToken cancellationToken)
    {
        if (ownerId == UserId)
        {
            await _transferMatchingService.MatchAroundAsync(affected, cancellationToken);
            return;
        }

        await using AsyncServiceScope scope = _scopeFactory.CreateUserScope(ownerId);
        await scope.ServiceProvider.GetRequiredService<ITransferMatchingService>()
            .MatchAroundAsync(affected, cancellationToken);
    }

    /// <summary>
    /// <paramref name="ids"/> and whatever they're paired with now, mapped, those on accounts the user can see.
    /// </summary>
    private async Task<IReadOnlyList<Transaction>> LoadAffectedAsync(HashSet<Guid> ids,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        AccountsInView view = await _accountScope.ResolveAllAsync(context, cancellationToken);
        List<BankTransaction> transactions = await context.BankTransactions.InView(view).AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        List<Guid> partners = transactions
            .Select(static x => x.Transfer?.PartnerTransactionId)
            .OfType<Guid>()
            .Where(x => !ids.Contains(x))
            .ToList();

        if (partners.Count > 0)
        {
            transactions.AddRange(await context.BankTransactions.InView(view)
                .AsNoTracking()
                .Where(x => partners.Contains(x.Id))
                .ToListAsync(cancellationToken));
        }

        TransactionMapper mapper = await TransactionMapper.LoadAsync(context, view, transactions,
            _timeProvider.LocalTimeZone, cancellationToken);
        return transactions.Select(mapper.ToContract).ToList();
    }
}
