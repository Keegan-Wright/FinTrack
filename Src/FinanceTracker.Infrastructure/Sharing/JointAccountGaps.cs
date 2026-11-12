using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// The hidden copy of a joint account filling in for the shown copy while the shown copy's connection has lapsed, see
/// <see cref="JointAccount.Gaps"/>.
/// </summary>
internal static class JointAccountGaps
{
    /// <summary>
    /// How long a connection's syncs fail before it counts as lapsed, as when consent was withdrawn at the bank, so a
    /// bank's brief outage doesn't open a gap.
    /// </summary>
    public static readonly TimeSpan FailingFor = TimeSpan.FromDays(3);

    /// <summary>
    /// Opens or closes <paramref name="joint"/>'s gap as its shown copy's connection has lapsed or synced again, then
    /// marks which of the hidden copy's transactions fill a gap. One that stops filling, as the shown copy now has it
    /// too, hands its tags, split and hidden bank classifications to the shown copy's, and its transfer is cleared.
    /// Tracked changes, saved by the caller.
    /// </summary>
    /// <returns>The transactions whose part in matching changed, to match again.</returns>
    public static async Task<List<Guid>> RefreshAsync(FinanceTrackerContext context, JointAccount joint, DateTime now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(joint);

        await UpdateGapAsync(context, joint, now, cancellationToken);
        if (joint.Gaps.Count == 0)
        {
            return [];
        }

        return await MarkFillingAsync(context, joint, now, cancellationToken);
    }

    /// <summary>
    /// Opens a gap when the shown copy's connection has lapsed, and closes it once the shown copy has synced its
    /// transactions again since.
    /// </summary>
    private static async Task UpdateGapAsync(FinanceTrackerContext context, JointAccount joint, DateTime now,
        CancellationToken cancellationToken)
    {
        Guid shownId = joint.ShownAccountId;
        BankConnection connection = await (
                from account in context.BankAccounts
                join bankConnection in context.BankConnections on account.ConnectionId equals bankConnection.Id
                where account.Id == shownId
                select bankConnection)
            .AsNoTracking()
            .SingleAsync(cancellationToken);

        bool lapsed = HasLapsed(connection, now);
        if (lapsed && !joint.HasOpenGap)
        {
            List<BankTransaction> shown = await SettledAsync(context, shownId, from: null, cancellationToken);
            DateTime from = shown.Count > 0 ? shown.Max(static x => x.OccurredAt) : DateTime.MinValue;
            joint.OpenGap(from, now);
            return;
        }

        if (lapsed || !joint.HasOpenGap)
        {
            return;
        }

        JointAccountGap open = joint.Gaps[^1];
        DateTime? syncedAt = await context.SyncLog.AsNoTracking()
            .Where(x => x.AccountId == shownId && x.Type == SyncType.Transactions && x.OccurredAt > open.OpenedAt)
            .MaxAsync(static x => (DateTime?)x.OccurredAt, cancellationToken);
        if (syncedAt is not { } synced)
        {
            return;
        }

        // Where its own transactions start again. What the bank wouldn't give back before then stays filled
        List<BankTransaction> since = await SettledAsync(context, shownId, open.From, cancellationToken);
        DateTime to = since.Where(x => x.OccurredAt > open.From)
            .Select(static x => x.OccurredAt)
            .DefaultIfEmpty(synced)
            .Min();
        if (to <= open.From)
        {
            to = synced;
        }

        joint.CloseGap(to, synced);
    }

    /// <summary>
    /// Whether <paramref name="connection"/> can no longer sync: its consent has lapsed, or its syncs have been
    /// failing for <see cref="FailingFor"/>.
    /// </summary>
    public static bool HasLapsed(BankConnection connection, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.HasConsentLapsed(now))
        {
            return true;
        }

        return connection.SyncFailingSince is { } failingSince && now - failingSince >= FailingFor;
    }

    /// <summary>
    /// Marks the hidden copy's transactions in a gap that the shown copy doesn't have as filling it, and unmarks the
    /// rest.
    /// </summary>
    private static async Task<List<Guid>> MarkFillingAsync(FinanceTrackerContext context, JointAccount joint,
        DateTime now, CancellationToken cancellationToken)
    {
        DateTime earliest = joint.Gaps.Min(static x => x.From);
        DateTime? from = earliest == DateTime.MinValue ? null : earliest;

        List<BankTransaction> hidden = await TrackedAsync(context, joint.HiddenAccountId, from, cancellationToken);
        List<BankTransaction> shown = await TrackedAsync(context, joint.ShownAccountId, from, cancellationToken);
        Dictionary<Guid, BankTransaction> onShown = JointAccounts.SameTransactions(hidden, shown)
            .ToDictionary(static x => x.From.Id, static x => x.To);

        List<Guid> changed = [];
        List<BankTransaction> stoppedFilling = [];
        List<BankTransaction> tookOver = [];
        foreach (BankTransaction transaction in hidden)
        {
            bool wasFilling = transaction.FillsJointGap;
            bool fills = joint.InGap(transaction.OccurredAt) && !onShown.ContainsKey(transaction.Id);
            if (!transaction.MarkFillsJointGap(fills))
            {
                continue;
            }

            changed.Add(transaction.Id);
            if (!wasFilling)
            {
                continue;
            }

            // The shown copy has it now, so what was put on it while it filled in carries over
            stoppedFilling.Add(transaction);
            if (onShown.TryGetValue(transaction.Id, out BankTransaction? same))
            {
                same.TakeAnnotationsFrom(transaction, withSplit: true);
                tookOver.Add(same);
                changed.Add(same.Id);
            }
        }

        List<BankTransaction> partners =
            await JointAccounts.ClearTransfersAsync(context, stoppedFilling, cancellationToken);
        changed.AddRange(partners.Select(static x => x.Id));

        if (tookOver.Count > 0)
        {
            UserLink link = await context.UserLinks.SingleAsync(x => x.Id == joint.LinkId, cancellationToken);
            await JointAccounts.ShareForeignTagsAsync(context, link, tookOver, now, cancellationToken);
        }

        return changed;
    }

    /// <summary>
    /// The settled transactions of <paramref name="accountId"/> on or after <paramref name="from"/>'s day, all of
    /// them when null, untracked.
    /// </summary>
    private static async Task<List<BankTransaction>> SettledAsync(FinanceTrackerContext context, Guid accountId,
        DateTime? from, CancellationToken cancellationToken)
    {
        List<BankTransaction> transactions = await context.BankTransactions.AsNoTracking()
            .Where(x => x.AccountId == accountId && !x.IsPending)
            .WhereEncryptedDayWithin(x => x.OccurredAt, from, null, context)
            .ToListAsync(cancellationToken);

        // Narrowed by day, so the exact time is checked once decrypted
        return transactions.Where(x => from is null || x.OccurredAt >= from.Value.Date).ToList();
    }

    /// <summary>
    /// The transactions of <paramref name="accountId"/> on or after <paramref name="from"/>'s day, all of them when
    /// null, tracked.
    /// </summary>
    private static async Task<List<BankTransaction>> TrackedAsync(FinanceTrackerContext context, Guid accountId,
        DateTime? from, CancellationToken cancellationToken)
    {
        List<BankTransaction> transactions = await context.BankTransactions
            .Where(x => x.AccountId == accountId)
            .WhereEncryptedDayWithin(x => x.OccurredAt, from, null, context)
            .ToListAsync(cancellationToken);

        return transactions.Where(x => from is null || x.OccurredAt >= from.Value.Date).ToList();
    }

    /// <summary>
    /// The joint accounts in <paramref name="view"/> that have had a gap, untracked.
    /// </summary>
    public static async Task<List<JointAccount>> InViewAsync(FinanceTrackerContext context, AccountsInView view,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(view);
        if (view.FillingAccounts.Count == 0)
        {
            return [];
        }

        IReadOnlyList<Guid> hiddenIds = view.FillingAccountIds;
        List<JointAccount> joints = await context.JointAccounts.AsNoTracking()
            .Where(x => hiddenIds.Contains(x.HiddenAccountId))
            .ToListAsync(cancellationToken);

        return joints.Where(static x => x.Gaps.Count > 0).ToList();
    }

    /// <summary>
    /// The balance of each joint account in <paramref name="view"/> with an open gap, by shown copy: the hidden copy's,
    /// as the shown copy's has stopped updating.
    /// </summary>
    public static async Task<Dictionary<Guid, AccountBalance?>> OpenGapBalancesAsync(FinanceTrackerContext context,
        AccountsInView view, CancellationToken cancellationToken)
    {
        List<JointAccount> open = (await InViewAsync(context, view, cancellationToken))
            .Where(static x => x.HasOpenGap)
            .ToList();
        if (open.Count == 0)
        {
            return [];
        }

        List<Guid> hiddenIds = open.Select(static x => x.HiddenAccountId).ToList();
        Dictionary<Guid, AccountBalance?> balances = await context.BankAccounts.AsNoTracking()
            .Where(x => hiddenIds.Contains(x.Id))
            .ToDictionaryAsync(static x => x.Id, static x => x.Balance, cancellationToken);

        return open.ToDictionary(static x => x.ShownAccountId, x => balances.GetValueOrDefault(x.HiddenAccountId));
    }
}
