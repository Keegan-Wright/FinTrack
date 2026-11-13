using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Sharing;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// Spotting, merging and splitting the joint accounts two linked people have both connected, see
/// <see cref="JointAccount"/>.
/// </summary>
internal static class JointAccounts
{
    /// <summary>
    /// Pairs of <paramref name="userId"/>'s accounts and the other person's in <paramref name="link"/> that are the
    /// same account, see <see cref="IsSameAccount"/>, one of them shared in the link, and neither already merged. Each
    /// account in one pair at most.
    /// </summary>
    public static async Task<List<(BankAccount Yours, BankAccount Theirs)>> SuggestionsAsync(
        FinanceTrackerContext context, UserLink link, Guid userId, CancellationToken cancellationToken)
    {
        Guid otherId = link.OtherThan(userId);
        List<Guid> merged = await MergedAccountIds(context).ToListAsync(cancellationToken);

        // Identifiers are encrypted, so they're compared once read
        List<BankAccount> accounts = await context.BankAccounts.AsNoTracking()
            .Where(x => (x.UserId == userId || x.UserId == otherId) && !merged.Contains(x.Id))
            .ToListAsync(cancellationToken);

        List<Guid> accountIds = accounts.Select(static x => x.Id).ToList();
        HashSet<Guid> shared = (await context.Shares.AsNoTracking()
                .Where(x => x.LinkId == link.Id && x.ItemType == ShareItemType.BankAccount &&
                            accountIds.Contains(x.ItemId))
                .Select(static x => x.ItemId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        return await PairAsync(context,
            accounts.Where(x => x.UserId == userId).ToList(),
            accounts.Where(x => x.UserId == otherId).ToList(),
            (yours, theirs) => shared.Contains(yours.Id) || shared.Contains(theirs.Id),
            cancellationToken);
    }

    /// <summary>
    /// Pairs each of <paramref name="yours"/> with the first of <paramref name="theirs"/> that <paramref
    /// name="allowed"/> lets it pair with and is the same account, see <see cref="IsSameAccount"/>. Each account in
    /// one pair at most.
    /// </summary>
    private static async Task<List<(BankAccount Yours, BankAccount Theirs)>> PairAsync(
        FinanceTrackerContext context, List<BankAccount> yours, List<BankAccount> theirs,
        Func<BankAccount, BankAccount, bool> allowed, CancellationToken cancellationToken)
    {
        if (yours.Count == 0 || theirs.Count == 0)
        {
            return [];
        }

        Dictionary<Guid, string> banks = await BanksAsync(context, [.. yours, .. theirs], cancellationToken);

        // Transactions are only read for the pots that could be the same, by bank, name, currency and kind
        List<(BankAccount Yours, BankAccount Theirs)> candidates = [];
        foreach (BankAccount your in yours)
        {
            foreach (BankAccount their in theirs)
            {
                if (allowed(your, their) && LooksAlike(your, their, banks))
                {
                    candidates.Add((your, their));
                }
            }
        }

        ILookup<Guid, (DateOnly, decimal, string)> transactions = await TransactionsOfUnnumberedAsync(context,
            candidates.SelectMany(static x => new[] { x.Yours, x.Theirs }).DistinctBy(static x => x.Id).ToList(),
            cancellationToken);

        List<(BankAccount Yours, BankAccount Theirs)> pairs = [];
        HashSet<Guid> taken = [];
        foreach ((BankAccount your, BankAccount their) in candidates)
        {
            if (taken.Contains(your.Id) || taken.Contains(their.Id))
            {
                continue;
            }

            if (IsSameAccount(your, their, transactions))
            {
                pairs.Add((your, their));
                taken.Add(your.Id);
                taken.Add(their.Id);
            }
        }

        return pairs;
    }

    /// <summary>
    /// Whether two people's accounts could be one account they've both connected, before their transactions are
    /// compared. The same sort code and number or IBAN when either has one, that settles it. A savings pot has
    /// neither, so then the same bank, name, currency and kind.
    /// </summary>
    private static bool LooksAlike(BankAccount yours, BankAccount theirs, Dictionary<Guid, string> banks)
    {
        if (yours.Identifier is not null || theirs.Identifier is not null)
        {
            return yours.Identifier?.IsSameAccountAs(theirs.Identifier) == true;
        }

        return banks.GetValueOrDefault(yours.ConnectionId) == banks.GetValueOrDefault(theirs.ConnectionId) &&
               string.Equals(yours.DisplayName.Trim(), theirs.DisplayName.Trim(),
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(yours.Currency, theirs.Currency, StringComparison.OrdinalIgnoreCase) &&
               yours.Kind == theirs.Kind;
    }

    /// <summary>
    /// Whether two accounts that <see cref="LooksAlike">look alike</see> are one account they've both connected. By
    /// sort code and number or IBAN when they have one. A savings pot, with neither, also needs the same transactions,
    /// or the same balance when neither has any yet. Someone confirms each merge, so a near miss costs them a look
    /// rather than a wrong merge.
    /// </summary>
    private static bool IsSameAccount(BankAccount yours, BankAccount theirs,
        ILookup<Guid, (DateOnly, decimal, string)> transactions)
    {
        if (yours.Identifier is not null)
        {
            return true;
        }

        List<(DateOnly, decimal, string)> ours = transactions[yours.Id].ToList();
        List<(DateOnly, decimal, string)> others = transactions[theirs.Id].ToList();
        if (ours.Count == 0 && others.Count == 0)
        {
            return yours.Balance?.Current == theirs.Balance?.Current;
        }

        return ours.Intersect(others).Any();
    }

    /// <summary>
    /// The bank each account's connection is to, by connection.
    /// </summary>
    private static async Task<Dictionary<Guid, string>> BanksAsync(FinanceTrackerContext context,
        List<BankAccount> accounts, CancellationToken cancellationToken)
    {
        List<Guid> connectionIds = accounts.Select(static x => x.ConnectionId).Distinct().ToList();
        return await context.BankConnections.AsNoTracking()
            .Where(x => connectionIds.Contains(x.Id))
            .ToDictionaryAsync(static x => x.Id, static x => x.ExternalProviderId, cancellationToken);
    }

    /// <summary>
    /// The settled transactions of the accounts with no sort code, number or IBAN, as compared across copies, by
    /// account.
    /// </summary>
    private static async Task<ILookup<Guid, (DateOnly, decimal, string)>> TransactionsOfUnnumberedAsync(
        FinanceTrackerContext context, List<BankAccount> accounts, CancellationToken cancellationToken)
    {
        List<Guid> unnumbered = accounts.Where(static x => x.Identifier is null).Select(static x => x.Id).ToList();
        if (unnumbered.Count == 0)
        {
            return Array.Empty<BankTransaction>().ToLookup(static x => x.AccountId, Key);
        }

        List<BankTransaction> transactions = await context.BankTransactions.AsNoTracking()
            .Where(x => unnumbered.Contains(x.AccountId) && !x.IsPending)
            .ToListAsync(cancellationToken);

        return transactions.ToLookup(static x => x.AccountId, Key);
    }

    /// <summary>
    /// Merges each pair in <paramref name="pairs"/>, the user's account then the other person's, the first connected
    /// shown and the other hidden. The hidden copy's tags and hidden bank classifications join the shown one's, its
    /// transfers are left for matching to find again from the shown one. Added to the context, saved by the caller.
    /// </summary>
    /// <exception cref="InvalidOperationException">One isn't among the link's suggestions.</exception>
    public static async Task<List<JointAccount>> MergeAsync(FinanceTrackerContext context, UserLink link,
        Guid userId, IReadOnlyCollection<(Guid Yours, Guid Theirs)> pairs, DateTime now,
        CancellationToken cancellationToken)
    {
        List<(BankAccount Yours, BankAccount Theirs)> suggestions =
            await SuggestionsAsync(context, link, userId, cancellationToken);

        List<JointAccount> merged = [];
        foreach ((Guid yourAccountId, Guid theirAccountId) in pairs)
        {
            if (!suggestions.Exists(x => x.Yours.Id == yourAccountId && x.Theirs.Id == theirAccountId))
            {
                throw new InvalidOperationException("Those two accounts can't be merged.");
            }

            (BankAccount yours, BankAccount theirs) = suggestions.Single(x => x.Yours.Id == yourAccountId);
            merged.Add(await MergeAsync(context, link, userId, yours, theirs, now, cancellationToken));
        }

        return merged;
    }

    private static async Task<JointAccount> MergeAsync(FinanceTrackerContext context, UserLink link, Guid userId,
        BankAccount yours, BankAccount theirs, DateTime now, CancellationToken cancellationToken)
    {
        BankAccount shown = yours;
        BankAccount hidden = theirs;
        if (ConnectedFirst(theirs, yours))
        {
            shown = theirs;
            hidden = yours;
        }

        await CopyAnnotationsAsync(context, link, hidden.Id, shown.Id, withSplits: false, now, cancellationToken);
        await ClearTransfersAsync(context, hidden.Id, cancellationToken);

        JointAccount joint = new(userId, link.Id, shown.Id, hidden.Id, now);
        context.JointAccounts.Add(joint);
        return joint;
    }

    /// <summary>
    /// Splits <paramref name="joint"/>, the hidden copy taking the shown one's tags, splits and hidden bank
    /// classifications as they stand. Added to the context, saved by the caller.
    /// </summary>
    public static async Task SplitAsync(FinanceTrackerContext context, UserLink link, JointAccount joint,
        DateTime now, CancellationToken cancellationToken)
    {
        await CopyAnnotationsAsync(context, link, joint.ShownAccountId, joint.HiddenAccountId, withSplits: true, now,
            cancellationToken);

        // Standing alone again, all of its transactions are its own to show
        Guid hiddenId = joint.HiddenAccountId;
        foreach (BankTransaction filling in await context.BankTransactions
                     .Where(x => x.AccountId == hiddenId && x.FillsJointGap)
                     .ToListAsync(cancellationToken))
        {
            filling.MarkFillsJointGap(false);
        }

        context.JointAccounts.Remove(joint);
    }

    /// <summary>
    /// Whether <paramref name="first"/> was connected before <paramref name="second"/>, by id when they were at the
    /// same moment, so it's always the same one.
    /// </summary>
    private static bool ConnectedFirst(BankAccount first, BankAccount second)
    {
        if (first.Created != second.Created)
        {
            return first.Created < second.Created;
        }

        return first.Id.CompareTo(second.Id) < 0;
    }

    /// <summary>
    /// Both copies of every merged joint account.
    /// </summary>
    public static IQueryable<Guid> MergedAccountIds(FinanceTrackerContext context) =>
        context.JointAccounts.Select(static x => x.ShownAccountId)
            .Concat(context.JointAccounts.Select(static x => x.HiddenAccountId));

    /// <summary>
    /// Copies the annotations on <paramref name="fromAccountId"/>'s transactions onto the same transactions in
    /// <paramref name="toAccountId"/>, each copy's own record of them from its own connection. Tags of the person who
    /// doesn't own the receiving copy are shared with its owner, so they see their names.
    /// </summary>
    private static async Task CopyAnnotationsAsync(FinanceTrackerContext context, UserLink link, Guid fromAccountId,
        Guid toAccountId, bool withSplits, DateTime now, CancellationToken cancellationToken)
    {
        List<BankTransaction> from = await context.BankTransactions.AsNoTracking()
            .Where(x => x.AccountId == fromAccountId)
            .ToListAsync(cancellationToken);
        List<BankTransaction> to = await context.BankTransactions
            .Where(x => x.AccountId == toAccountId)
            .ToListAsync(cancellationToken);

        List<(BankTransaction From, BankTransaction To)> pairs = SameTransactions(from, to);
        foreach ((BankTransaction source, BankTransaction target) in pairs)
        {
            target.TakeAnnotationsFrom(source, withSplits);
        }

        await ShareForeignTagsAsync(context, link, pairs.Select(static x => x.To).ToList(), now, cancellationToken);
    }

    /// <summary>
    /// Each of <paramref name="from"/> with the same transaction in <paramref name="to"/>. The two copies come from
    /// different connections, whose ids for a transaction differ, so they're paired by day, amount and description.
    /// Two logins can describe one transaction differently, so what's left is paired by day and amount when that's
    /// the only one on each side. Settled ones only, a pending one may still change.
    /// </summary>
    internal static List<(BankTransaction From, BankTransaction To)> SameTransactions(List<BankTransaction> from,
        List<BankTransaction> to)
    {
        Dictionary<(DateOnly, decimal, string), Queue<BankTransaction>> waiting = to
            .Where(static x => !x.IsPending)
            .OrderBy(static x => x.OccurredAt)
            .GroupBy(Key)
            .ToDictionary(static x => x.Key, static x => new Queue<BankTransaction>(x));

        List<(BankTransaction From, BankTransaction To)> pairs = [];
        foreach (BankTransaction source in from.Where(static x => !x.IsPending).OrderBy(static x => x.OccurredAt))
        {
            if (waiting.TryGetValue(Key(source), out Queue<BankTransaction>? candidates) &&
                candidates.TryDequeue(out BankTransaction? target))
            {
                pairs.Add((source, target));
            }
        }

        pairs.AddRange(OnlyOnesOfTheirDayAndAmount(from, to, pairs));
        return pairs;
    }

    /// <summary>
    /// Pairs of <paramref name="from"/> and <paramref name="to"/> not in <paramref name="paired"/> that are each the
    /// only one left on their side with that day and amount, so their descriptions differing can't mix two up.
    /// </summary>
    private static List<(BankTransaction From, BankTransaction To)> OnlyOnesOfTheirDayAndAmount(
        List<BankTransaction> from, List<BankTransaction> to, List<(BankTransaction From, BankTransaction To)> paired)
    {
        HashSet<Guid> pairedIds = paired.SelectMany(static x => new[] { x.From.Id, x.To.Id }).ToHashSet();
        Dictionary<(DateOnly, decimal), List<BankTransaction>> leftOnTo = to
            .Where(x => !x.IsPending && !pairedIds.Contains(x.Id))
            .GroupBy(DayAndAmount)
            .ToDictionary(static x => x.Key, static x => x.ToList());

        List<(BankTransaction From, BankTransaction To)> pairs = [];
        IEnumerable<IGrouping<(DateOnly, decimal), BankTransaction>> leftOnFrom = from
            .Where(x => !x.IsPending && !pairedIds.Contains(x.Id))
            .GroupBy(DayAndAmount);
        foreach (IGrouping<(DateOnly, decimal), BankTransaction> sources in leftOnFrom)
        {
            if (sources.Count() != 1 || !leftOnTo.TryGetValue(sources.Key, out List<BankTransaction>? targets) ||
                targets.Count != 1)
            {
                continue;
            }

            pairs.Add((sources.Single(), targets[0]));
        }

        return pairs;
    }

    private static (DateOnly Day, decimal Amount) DayAndAmount(BankTransaction transaction) =>
        (DateOnly.FromDateTime(transaction.OccurredAt), transaction.Amount);

    private static (DateOnly Day, decimal Amount, string Description) Key(BankTransaction transaction) =>
        (DateOnly.FromDateTime(transaction.OccurredAt), transaction.Amount,
            string.Join(' ', transaction.Description.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .ToUpperInvariant());

    /// <summary>
    /// Shares the tags on <paramref name="transactions"/> that are the other person's in <paramref name="link"/> with
    /// each transaction's owner, so they see their names.
    /// </summary>
    internal static async Task ShareForeignTagsAsync(FinanceTrackerContext context, UserLink link,
        List<BankTransaction> transactions, DateTime now, CancellationToken cancellationToken)
    {
        List<Guid> tagIds = transactions.SelectMany(static x => x.CustomClassificationIds).Distinct().ToList();
        if (tagIds.Count == 0)
        {
            return;
        }

        Dictionary<Guid, Guid> tagOwners = await context.CustomClassifications.AsNoTracking()
            .Where(x => tagIds.Contains(x.Id))
            .ToDictionaryAsync(static x => x.Id, static x => x.UserId, cancellationToken);

        foreach (BankTransaction transaction in transactions)
        {
            foreach (Guid tagId in transaction.CustomClassificationIds)
            {
                if (!tagOwners.TryGetValue(tagId, out Guid ownerId) || ownerId == transaction.UserId ||
                    !link.Involves(ownerId))
                {
                    continue;
                }

                await AnnotationShares.ShareWithOwnerAsync(context, ownerId, transaction,
                    ShareItemType.Classification, tagId, now, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Takes the transfer answers off the hidden copy's transactions, and the other halves paired with them, matching
    /// finds the pairs again from the shown copy.
    /// </summary>
    private static async Task ClearTransfersAsync(FinanceTrackerContext context, Guid hiddenAccountId,
        CancellationToken cancellationToken)
    {
        List<BankTransaction> hidden = await context.BankTransactions
            .Where(x => x.AccountId == hiddenAccountId && x.Transfer != null)
            .ToListAsync(cancellationToken);
        await ClearTransfersAsync(context, hidden, cancellationToken);
    }

    /// <summary>
    /// Takes the transfer answers off <paramref name="hidden"/>, tracked, and the other halves paired with them, for
    /// transactions of a hidden copy leaving the views.
    /// </summary>
    /// <returns>The other halves cleared.</returns>
    internal static async Task<List<BankTransaction>> ClearTransfersAsync(FinanceTrackerContext context,
        IReadOnlyCollection<BankTransaction> hidden, CancellationToken cancellationToken)
    {
        List<Guid> hiddenIds = hidden.Where(static x => x.Transfer is not null).Select(static x => x.Id).ToList();
        if (hiddenIds.Count == 0)
        {
            return [];
        }

        List<BankTransaction> partners = await context.BankTransactions
            .Where(x => x.Transfer != null && x.Transfer.PartnerTransactionId != null &&
                        hiddenIds.Contains(x.Transfer.PartnerTransactionId.Value))
            .ToListAsync(cancellationToken);

        foreach (BankTransaction transaction in hidden.Concat(partners))
        {
            transaction.ClearTransfer(DecisionSource.User);
        }

        return partners;
    }
}
