using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Household;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.OpenBanking.Transfers;

/// <summary>
/// Decides which of a user's transactions are money moved between their own accounts, recording the decision on both
/// halves of a pair. Four steps, each only looking at what the steps before it left undecided:
/// <list type="number">
/// <item>The bank's reference names another of the user's accounts, a transfer, paired with its mirror in that
/// account when there's exactly one.</item>
/// <item>An equal and opposite amount on another account within <see cref="MaxDaysApart"/> days a transfer when the
/// provider labels either half one (<see cref="BankTransaction.LabelledAsTransfer"/>) or the money comes in on a card
/// (a repayment), otherwise up for review. Equally good candidates are all put up for review rather than guessed.
/// </item>
/// <item>A household member is recognised (<see cref="HouseholdMember.Recognises"/>), a transfer to or from them.
/// </item>
/// <item>Anything else the provider labels a transfer is put up for review.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// Only transactions with no decision or the matcher's own are decided. The user's and rules' decisions, and the
/// partners they name, are left alone and never paired with anything else. Pending transactions are skipped until
/// booked, as a pending one can disappear. Each transaction is in at most one pair.
/// </para>
/// <para>
/// Every run works out the matcher's decisions from scratch, then compares them with what's stored new ones are
/// recorded, unchanged ones left as they are, and stored ones it no longer reaches are cleared. So running again over
/// the same transactions changes nothing, and a match whose reason has gone (the partner was removed, or the user
/// said it isn't a transfer) undoes itself.
/// </para>
/// </remarks>
public static class TransferMatcher
{
    /// <summary>
    /// How many calendar days apart the halves of a transfer can be. Some banks only give a date, and a payment
    /// between banks can land a day or two later.
    /// </summary>
    public const int MaxDaysApart = 2;

    /// <summary>
    /// Matches <paramref name="transactions"/> against their
    /// <paramref name="accounts"/> and the user's <paramref name="householdMembers"/>.
    /// </summary>
    public static TransferMatchSummary Match(IReadOnlyCollection<BankAccount> accounts,
        IReadOnlyCollection<HouseholdMember> householdMembers, IReadOnlyCollection<BankTransaction> transactions,
        DateTime decidedAt)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(householdMembers);
        ArgumentNullException.ThrowIfNull(transactions);

        // A decision the user or a rule made can name a partner, e.g. when the user confirms a match. That partner
        // still carries the matcher's decision, but belongs to the user's pair now, so it mustn't be re-decided or
        // paired with anything else
        HashSet<Guid> lockedPartners = transactions
            .Where(static x => x.Transfer is { Source: > DecisionSource.Matcher })
            .Select(static x => x.Transfer!.PartnerTransactionId)
            .OfType<Guid>()
            .ToHashSet();

        // What this run may decide. Oldest first so earlier transfers pair before later ones, and by id after that so
        // the same transactions always give the same result
        List<BankTransaction> open = transactions
            .Where(x => !x.IsPending && x.Transfer is null or { Source: DecisionSource.Matcher } &&
                        !lockedPartners.Contains(x.Id))
            .OrderBy(static x => x.OccurredAt)
            .ThenBy(static x => x.Id)
            .ToList();

        // The run's decisions, by transaction. Nothing is written to a transaction until Apply, and a transaction in
        // here is taken, which keeps every transaction to one pair later steps and candidates skip it
        Dictionary<Guid, TransferInfo> decisions = [];

        // Step 1
        MatchOwnAccountReferences(accounts, open, decisions, decidedAt);

        // Step 2
        MatchMirrorPairs(accounts, open, decisions, decidedAt);

        // Step 3
        MatchHouseholdMembers(householdMembers, open, decisions, decidedAt);

        // Step 4, labelled a transfer but nothing on the user's accounts matches it. Often a real payment to someone
        // else, so it stays counted until the user says otherwise
        foreach (BankTransaction transaction in open.Where(x => !decisions.ContainsKey(x.Id) && x.LabelledAsTransfer))
        {
            decisions[transaction.Id] =
                TransferInfo.ToReview(TransferReason.BankTransferLabel, null, null, decidedAt);
        }

        return Apply(open, decisions);
    }

    /// <summary>
    /// Step 1. A reference naming one of the user's accounts is the strongest sign there is, so the transaction is a
    /// transfer whether or not its other half has arrived yet.
    /// </summary>
    private static void MatchOwnAccountReferences(IReadOnlyCollection<BankAccount> accounts,
        List<BankTransaction> open, Dictionary<Guid, TransferInfo> decisions, DateTime decidedAt)
    {
        foreach (BankTransaction transaction in open)
        {
            // Already decided as the mirror of an earlier transaction in this loop, or names none of the accounts
            if (decisions.ContainsKey(transaction.Id) || NamedAccount(accounts, transaction) is not { } named)
            {
                continue;
            }

            // The account is known, so the other half is only looked for in that account. Without exactly one
            // closest, it's linked to the account alone, and a later run pairs it once the mirror arrives or the tie
            // is gone
            List<BankTransaction> mirrors =
                ClosestMirrors(transaction, open.Where(x => x.AccountId == named.Id), decisions);
            BankTransaction? mirror = mirrors.Count == 1 ? mirrors[0] : null;

            decisions[transaction.Id] =
                TransferInfo.Matched(TransferReason.OwnAccountReference, mirror?.Id, named.Id, decidedAt);
            if (mirror is not null)
            {
                // The mirror is matched for the same reason, though its own reference may not name anything
                decisions[mirror.Id] = TransferInfo.Matched(TransferReason.OwnAccountReference, transaction.Id,
                    transaction.AccountId, decidedAt);
            }
        }
    }

    /// <summary>
    /// Step 2. Pairs each outgoing transaction with an equal incoming one on another account, but only when the two
    /// are each other's closest. Driven from the outgoing side, so each pair is considered once.
    /// </summary>
    /// <example>
    /// −£50 Monday 09:00 from current, +£50 Tuesday 09:05 into savings, −£50 Tuesday 09:00 from joint. For Monday's,
    /// savings is the closest incoming. But savings' closest outgoing is joint's, five minutes away, so Monday's is
    /// skipped and joint pairs with savings in its turn. Monday's is left unpaired rather than taking a partner that
    /// fits another better.
    /// </example>
    private static void MatchMirrorPairs(IReadOnlyCollection<BankAccount> accounts, List<BankTransaction> open,
        Dictionary<Guid, TransferInfo> decisions, DateTime decidedAt)
    {
        HashSet<Guid> cards = accounts
            .Where(static x => x.Kind == BankAccountKind.Card)
            .Select(static x => x.Id)
            .ToHashSet();

        foreach (BankTransaction outgoing in open.Where(static x => x.Amount < 0))
        {
            // Taken by step 1, or put up for review as part of an earlier tie
            if (decisions.ContainsKey(outgoing.Id))
            {
                continue;
            }

            List<BankTransaction> incoming = ClosestMirrors(outgoing, open, decisions);
            if (incoming.Count == 0)
            {
                continue;
            }

            // Look back from the incoming side, which outgoing ones is it closest to? Only worked out when there's a
            // single incoming candidate, with several it's a tie whatever the answer
            List<BankTransaction> rivals = incoming.Count == 1
                ? ClosestMirrors(incoming[0], open, decisions)
                : [];

            // A tie on either side, several equally close incoming ones, or this outgoing one and another equally
            // close to the incoming. Picking one would be a guess, so all of them go to review. Being in decisions also
            // takes them out of any later pairing this run
            if (incoming.Count > 1 || rivals.Count > 1)
            {
                foreach (BankTransaction ambiguous in incoming.Concat(rivals).Append(outgoing).Distinct())
                {
                    decisions[ambiguous.Id] =
                        TransferInfo.ToReview(TransferReason.AmbiguousMirrorPair, null, null, decidedAt);
                }

                continue;
            }

            // The incoming one's closest is a different outgoing one, so it isn't this one's to take, see the example
            // above. Usually that one comes later in the loop and pairs with it. If it came earlier and was passed over
            // for its own closest, the incoming is left unpaired this run rather than settling for second best
            if (rivals[0] != outgoing)
            {
                continue;
            }

            // Mutually closest. Equal amounts on the same day happen by chance, paying a friend £20 while someone pays
            // you £20, so the provider has to call at least one half a transfer before it leaves the totals. Money
            // coming in on one of the user's cards from their own account is a repayment
            // so that's enough too. Otherwise the pair is suggested for review
            BankTransaction partner = incoming[0];
            bool repayment = cards.Contains(partner.AccountId);
            bool confident = outgoing.LabelledAsTransfer || partner.LabelledAsTransfer || repayment;

            decisions[outgoing.Id] = Decide(confident, partner, decidedAt);
            decisions[partner.Id] = Decide(confident, outgoing, decidedAt);
        }
    }

    /// <summary>
    /// Step 3. Money to or from a household member whose account isn't connected, so there's no other half to find.
    /// Naming the member is a stronger sign than the bank's label, so the label isn't needed. After the user's own
    /// accounts so a pair found there always wins, e.g. once the member's account is connected.
    /// </summary>
    private static void MatchHouseholdMembers(IReadOnlyCollection<HouseholdMember> householdMembers,
        List<BankTransaction> open, Dictionary<Guid, TransferInfo> decisions, DateTime decidedAt)
    {
        foreach (BankTransaction transaction in open.Where(x => !decisions.ContainsKey(x.Id)))
        {
            // Several members recognising it, e.g. sharing a payee name, would be a guess, so like NamedAccount it
            // stops at two and only one counts
            if (householdMembers
                    .Where(x => x.Recognises(transaction.Description, transaction.Metadata.ProviderReference))
                    .Take(2)
                    .ToList() is [var member])
            {
                decisions[transaction.Id] = TransferInfo.WithHouseholdMember(member.Id, decidedAt);
            }
        }
    }

    /// <summary>
    /// A mirror pair's decision for one half, naming the other half as its partner either way so a review can show
    /// the pair side by side.
    /// </summary>
    private static TransferInfo Decide(bool confident, BankTransaction partner, DateTime decidedAt) => confident
        ? TransferInfo.Matched(TransferReason.MirrorPair, partner.Id, partner.AccountId, decidedAt)
        : TransferInfo.ToReview(TransferReason.MirrorPair, partner.Id, partner.AccountId, decidedAt);

    /// <summary>
    /// The undecided mirrors of <paramref name="transaction"/> among <paramref name="candidates"/> that are closest
    /// in time, more than one when they're equally close.
    /// </summary>
    /// <remarks>
    /// Closeness is the exact time apart, while <see cref="IsMirror"/> limits candidates by calendar day. For a bank
    /// that only gives dates every transaction sits at midnight, so two on the same day are equally close and tie.
    /// </remarks>
    private static List<BankTransaction> ClosestMirrors(BankTransaction transaction,
        IEnumerable<BankTransaction> candidates, Dictionary<Guid, TransferInfo> decisions)
    {
        List<(BankTransaction Candidate, TimeSpan Apart)> mirrors = candidates
            .Where(x => !decisions.ContainsKey(x.Id) && IsMirror(transaction, x))
            .Select(x => (x, (x.OccurredAt - transaction.OccurredAt).Duration()))
            .ToList();

        if (mirrors.Count == 0)
        {
            return [];
        }

        TimeSpan closest = mirrors.Min(static x => x.Apart);
        return mirrors.Where(x => x.Apart == closest).Select(static x => x.Candidate).ToList();
    }

    /// <summary>
    /// Whether <paramref name="candidate"/> could be the other half of <paramref name="transaction"/> another
    /// account, the exact opposite amount in the same currency, and within <see cref="MaxDaysApart"/> calendar days.
    /// </summary>
    /// <remarks>
    /// Calendar days rather than hours, as banks that only give a date put everything at midnight. A zero amount
    /// mirrors itself, e.g. a card check, so it never matches.
    /// </remarks>
    private static bool IsMirror(BankTransaction transaction, BankTransaction candidate) =>
        candidate.AccountId != transaction.AccountId &&
        candidate.Amount == -transaction.Amount &&
        transaction.Amount != 0 &&
        string.Equals(candidate.Currency, transaction.Currency, StringComparison.OrdinalIgnoreCase) &&
        Math.Abs((candidate.OccurredAt.Date - transaction.OccurredAt.Date).Days) <= MaxDaysApart;

    /// <summary>
    /// The one other account of the user's that the transaction's reference names, null when none or several do.
    /// </summary>
    private static BankAccount? NamedAccount(IReadOnlyCollection<BankAccount> accounts, BankTransaction transaction)
    {
        // Several accounts sharing an identifier, e.g. pots reporting their parent's number, would be a guess. Two is
        // enough to know it isn't exactly one, so it stops looking there
        return accounts
            .Where(x => x.Id != transaction.AccountId &&
                        x.Identifier?.IsNamedIn(transaction.Metadata.ProviderReference) == true)
            .Take(2)
            .ToList() is [var only] ? only : null;
    }

    /// <summary>
    /// Writes the run's decisions to the transactions, and clears the matcher's earlier decisions the run no longer
    /// makes.
    /// </summary>
    /// <returns>What changed. Decisions that were already stored aren't counted.</returns>
    private static TransferMatchSummary Apply(List<BankTransaction> open, Dictionary<Guid, TransferInfo> decisions)
    {
        int transfers = 0, toReview = 0, cleared = 0;

        foreach (BankTransaction transaction in open)
        {
            // Nothing matched it this time. Only the matcher's own decision can be cleared, which in an open
            // transaction is the only kind there is
            if (!decisions.TryGetValue(transaction.Id, out TransferInfo? decision))
            {
                cleared += transaction.ClearTransfer(DecisionSource.Matcher) ? 1 : 0;
                continue;
            }

            // Already stored, left as it is so it keeps when it was first decided and isn't saved again
            if (decision.IsSameAs(transaction.Transfer))
            {
                continue;
            }

            transaction.DecideTransfer(decision);
            if (decision.Decision == TransferDecision.Transfer)
            {
                transfers++;
            }
            else
            {
                toReview++;
            }
        }

        return new TransferMatchSummary(transfers, toReview, cleared);
    }
}
