using System.Text;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.Bills;

/// <summary>
/// Finds the payments of a user's bills among their transactions. A payment matches a bill when it's money out of the
/// bill's account, to its payee, within <see cref="WindowDays"/> of a due date and within
/// <see cref="AmountBandPercent"/> of the expected amount, so a price change still matches and shows as
/// <see cref="BillState.Changed"/>.
/// </summary>
/// <remarks>
/// Each period takes one payment and each payment pays one bill, the closest in amount then in date winning. Rules'
/// and the user's decisions are never replaced, and the periods they paid are left to them. Running again gives the
/// same result, and a match that no longer fits is taken back.
/// </remarks>
public static class BillMatcher
{
    /// <summary>
    /// How many days either side of its due date a payment can land, for weekends, bank holidays and a direct debit's
    /// guessed schedule.
    /// </summary>
    public const int WindowDays = 5;

    /// <summary>
    /// How far from the expected amount a payment can be and still match, as a percentage of it.
    /// </summary>
    public const decimal AmountBandPercent = 50;

    /// <summary>
    /// A transaction's name shorter than this has to contain the payee rather than the other way round, so e.g.
    /// <c>SKY</c> doesn't match every payee with sky in it.
    /// </summary>
    private const int MinimumNameLength = 4;

    /// <summary>
    /// Matches <paramref name="transactions"/> to <paramref name="bills"/> and records the decisions on them.
    /// </summary>
    /// <param name="bills">The user's bills.</param>
    /// <param name="transactions">The transactions to match, with any around them that already pay the bills so
    /// their periods are known to be paid.</param>
    /// <param name="timeZone">Where the user is, so a payment falls on the day it was where they are.</param>
    /// <returns>How many transactions changed.</returns>
    public static int Match(IReadOnlyCollection<Bill> bills, IEnumerable<BankTransaction> transactions,
        TimeZoneInfo timeZone, DateTime decidedAt)
    {
        ArgumentNullException.ThrowIfNull(bills);
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(timeZone);

        List<BankTransaction> all = transactions.ToList();
        Dictionary<Guid, Bill> billsById = bills.ToDictionary(static x => x.Id);

        HashSet<(Guid BillId, DateOnly Due)> paid = PaidByOthers(all, billsById, timeZone);
        List<BankTransaction> open = all.Where(IsOpen).ToList();

        List<BillCandidate> candidates = open
            .SelectMany(x => Candidates(x, bills, timeZone))
            .OrderBy(static x => x.AmountGap)
            .ThenBy(static x => x.DayGap)
            .ToList();

        Dictionary<Guid, Guid> billFor = [];
        foreach (BillCandidate candidate in candidates)
        {
            if (billFor.ContainsKey(candidate.Transaction.Id) || !paid.Add((candidate.Bill.Id, candidate.Due)))
            {
                continue;
            }

            billFor.Add(candidate.Transaction.Id, candidate.Bill.Id);
        }

        int changed = 0;
        foreach (BankTransaction transaction in open)
        {
            if (Record(transaction, billFor.GetValueOrDefault(transaction.Id), decidedAt))
            {
                changed++;
            }
        }

        return changed;
    }

    /// <summary>
    /// Whether <paramref name="transaction"/> is paid to <paramref name="bill"/>'s payee, by its description or the
    /// merchant or counterparty the bank named, ignoring case, spaces and punctuation.
    /// </summary>
    public static bool IsPaidTo(Bill bill, BankTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(bill);
        ArgumentNullException.ThrowIfNull(transaction);

        string payee = Normalise(bill.Payee);
        if (payee.Length == 0)
        {
            return false;
        }

        foreach (string name in Names(transaction))
        {
            if (name.Contains(payee, StringComparison.Ordinal))
            {
                return true;
            }

            if (name.Length >= MinimumNameLength && payee.Contains(name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the matcher can decide for <paramref name="transaction"/> money out no rule or user has decided about.
    /// </summary>
    private static bool IsOpen(BankTransaction transaction) =>
        transaction.Amount < 0 &&
        (transaction.BillPayment is null || transaction.BillPayment.Source == DecisionSource.Matcher);

    /// <summary>
    /// The periods rules and the user have already paid, which the matcher leaves to them.
    /// </summary>
    private static HashSet<(Guid BillId, DateOnly Due)> PaidByOthers(List<BankTransaction> transactions,
        Dictionary<Guid, Bill> bills, TimeZoneInfo timeZone)
    {
        HashSet<(Guid BillId, DateOnly Due)> paid = [];
        foreach (BankTransaction transaction in transactions)
        {
            BillPayment? payment = transaction.BillPayment;
            if (payment is not { BillId: { } billId } || payment.Source == DecisionSource.Matcher)
            {
                continue;
            }

            if (bills.TryGetValue(billId, out Bill? bill))
            {
                paid.Add((billId, bill.NearestDue(transaction.OccurredAt.LocalDayIn(timeZone))));
            }
        }

        return paid;
    }

    private static IEnumerable<BillCandidate> Candidates(BankTransaction transaction, IReadOnlyCollection<Bill> bills,
        TimeZoneInfo timeZone)
    {
        DateOnly day = transaction.OccurredAt.LocalDayIn(timeZone);
        decimal amount = -transaction.Amount;

        foreach (Bill bill in bills)
        {
            if (bill.AccountId != transaction.AccountId || !IsPaidTo(bill, transaction))
            {
                continue;
            }

            DateOnly due = bill.NearestDue(day);
            int dayGap = Math.Abs(day.DayNumber - due.DayNumber);
            if (dayGap > WindowDays || bill.EndedOn < due)
            {
                continue;
            }

            decimal expected = bill.ExpectedOn(due);
            decimal amountGap = Math.Abs(amount - expected) / expected;
            if (amountGap * 100 > AmountBandPercent)
            {
                continue;
            }

            yield return new BillCandidate(transaction, bill, due, amountGap, dayGap);
        }
    }

    /// <returns>Whether the transaction changed.</returns>
    private static bool Record(BankTransaction transaction, Guid billId, DateTime decidedAt)
    {
        if (billId == Guid.Empty)
        {
            return transaction.ClearBillPayment(DecisionSource.Matcher);
        }

        BillPayment payment = BillPayment.Matched(billId, decidedAt);
        if (payment.IsSameAs(transaction.BillPayment))
        {
            return false;
        }

        return transaction.DecideBillPayment(payment);
    }

    private static IEnumerable<string> Names(BankTransaction transaction)
    {
        string?[] names =
        [
            transaction.Description, transaction.Metadata.MerchantName, transaction.Metadata.CounterPartyPreferredName
        ];

        foreach (string? name in names)
        {
            string normalised = Normalise(name);
            if (normalised.Length > 0)
            {
                yield return normalised;
            }
        }
    }

    /// <summary>
    /// Upper case letters and digits only, so <c>Netflix.com</c> and <c>NETFLIX COM</c> read the same.
    /// </summary>
    private static string Normalise(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        StringBuilder builder = new(text.Length);
        foreach (char character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString();
    }
}
