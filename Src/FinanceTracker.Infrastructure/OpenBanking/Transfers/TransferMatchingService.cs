using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.OpenBanking.Transfers;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ITransferMatchingService>]
public class TransferMatchingService : ServiceBase<TransferMatchingService>, ITransferMatchingService
{
    private readonly TimeProvider _timeProvider;

    public TransferMatchingService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<TransferMatchingService> logger,
        TimeProvider timeProvider) : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
    }

    public async Task<TransferMatchSummary> MatchAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Amounts and dates are encrypted, so matching happens in memory. Over everything the user has rather than a
        // recent window, as the matcher treats what it's given as the whole picture: a stored match whose partner
        // isn't in the set would be cleared. Tracked, so saving writes only the transactions the matcher changed
        List<BankAccount> accounts = await context.BankAccounts.OwnedBy(UserId).ToListAsync(cancellationToken);
        List<BankTransaction> transactions =
            await context.BankTransactions.OwnedBy(UserId).ToListAsync(cancellationToken);

        TransferMatchSummary summary =
            TransferMatcher.Match(accounts, transactions, _timeProvider.GetUtcNow().UtcDateTime);

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogMatchedTransfers(UserId, summary.Transfers, summary.ToReview, summary.Cleared);

        return summary;
    }
}
