using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.OpenBanking;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IAccountSnapshotWriter>]
public sealed class AccountSnapshotWriter : IAccountSnapshotWriter
{
    private readonly ILogger<AccountSnapshotWriter> _logger;

    public AccountSnapshotWriter(ILogger<AccountSnapshotWriter> logger)
    {
        _logger = logger;
    }

    public async Task ApplyAsync(FinanceTrackerContext context, BankAccount account, AccountSnapshot snapshot,
        DateTime syncedAt, ConnectionSyncTally tally, CancellationToken cancellationToken)
    {
        if (snapshot.Balances is not null)
        {
            if (snapshot.Balances.FirstOrDefault() is { } balance)
            {
                AccountBalance mapped = ToBalance(account, balance);
                account.UpdateBalance(mapped.Available, mapped.Current, mapped.AsOf, mapped.CreditLimit);
            }

            context.SyncLog.Add(account.RecordSync(SyncType.Balance, syncedAt));
        }

        if (snapshot.StandingOrders is not null)
        {
            account.SyncStandingOrders(snapshot.StandingOrders.Select(static x => x.ToDetails()).ToList());
            context.SyncLog.Add(account.RecordSync(SyncType.StandingOrders, syncedAt));
        }

        if (snapshot.DirectDebits is not null)
        {
            account.SyncDirectDebits(snapshot.DirectDebits.Select(static x => x.ToDetails()).ToList());
            context.SyncLog.Add(account.RecordSync(SyncType.DirectDebits, syncedAt));
        }

        if (snapshot.Transactions is null && snapshot.PendingTransactions is null)
        {
            return;
        }

        // Pending first so a transaction reported as both is stored as booked
        Dictionary<string, TransactionDetails> incoming = [];
        foreach (ExternalOpenBankingAccountTransaction pending in snapshot.PendingTransactions ?? [])
        {
            incoming[pending.TransactionId] = await ToDetailsAsync(account, pending, true, cancellationToken);
        }

        foreach (ExternalOpenBankingAccountTransaction booked in snapshot.Transactions ?? [])
        {
            incoming[booked.TransactionId] = await ToDetailsAsync(account, booked, false, cancellationToken);
        }

        List<string> externalIds = incoming.Keys.ToList();
        Dictionary<string, BankTransaction> existing = await context.BankTransactions
            .Where(x => x.AccountId == account.Id)
            .WhereEncryptedIn(x => x.ExternalId, externalIds, context)
            .ToDictionaryAsync(static x => x.ExternalId, cancellationToken);

        foreach (TransactionDetails details in incoming.Values)
        {
            if (existing.TryGetValue(details.ExternalId, out BankTransaction? transaction))
            {
                transaction.UpdateFromSync(details);
                tally.TransactionsUpdated++;
            }
            else
            {
                context.BankTransactions.Add(BankTransaction.Import(account, details));
                tally.TransactionsAdded++;
            }
        }

        // A failed pending fetch is null, so stored pending rows are only removed against a list we actually got
        if (snapshot.PendingTransactions is not null)
        {
            tally.PendingRemoved +=
                await RemoveStalePendingTransactionsAsync(context, account, incoming.Keys, cancellationToken);
        }

        // Only once booked transactions are in, the log marks where the next sync fetches from
        if (snapshot.Transactions is not null)
        {
            context.SyncLog.Add(account.RecordSync(SyncType.Transactions, syncedAt));
        }
    }

    /// <summary>
    /// The balance in our terms, a card's being what's owed as a negative amount.
    /// </summary>
    private static AccountBalance ToBalance(BankAccount account, ExternalOpenBankingAccountBalance balance)
    {
        if (account.Kind == BankAccountKind.Card)
        {
            return balance.ToCardBalance();
        }

        return balance.ToAccountBalance();
    }

    /// <summary>
    /// The transaction in our terms, a card's signed by its type.
    /// </summary>
    private static Task<TransactionDetails> ToDetailsAsync(BankAccount account,
        ExternalOpenBankingAccountTransaction transaction, bool isPending, CancellationToken cancellationToken)
    {
        if (account.Kind == BankAccountKind.Card)
        {
            return transaction.ToCardDetailsAsync(isPending, cancellationToken);
        }

        return transaction.ToDetailsAsync(isPending, cancellationToken);
    }

    /// <summary>
    /// Removes pending transactions the provider no longer reports. A bank can settle a pending transaction under a
    /// different id, which would otherwise leave the pending copy counted alongside the booked one.
    /// </summary>
    /// <returns>How many were removed.</returns>
    private async Task<int> RemoveStalePendingTransactionsAsync(FinanceTrackerContext context, BankAccount account,
        IReadOnlyCollection<string> reportedExternalIds, CancellationToken cancellationToken)
    {
        // ExternalId is encrypted, so the account's pending rows are loaded and compared in memory
        List<BankTransaction> stale = (await context.BankTransactions
                .Where(x => x.AccountId == account.Id && x.IsPending)
                .ToListAsync(cancellationToken))
            .Where(x => !reportedExternalIds.Contains(x.ExternalId))
            .ToList();

        if (stale.Count == 0)
        {
            return 0;
        }

        context.BankTransactions.RemoveRange(stale);
        _logger.LogRemovedStalePendingTransactions(stale.Count, account.Id, account.UserId);
        return stale.Count;
    }
}
