using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.Bills;

/// <summary>
/// A regular payment out of an account, e.g. rent by standing order or a card subscription, due every
/// <see cref="Frequency"/> from <see cref="StartsOn"/>.
/// </summary>
/// <remarks>
/// Its expected amount is kept per period from when each one was set, see <see cref="ChangeAmount"/>, so past periods
/// keep the price they had. Which transactions paid it is kept on each <see cref="BankTransaction.BillPayment"/>, and
/// how each period stands is never stored, <see cref="BillCalculator"/> works it out.
/// </remarks>
public sealed class Bill : AggregateRoot
{
    /// <summary>
    /// How far a payment can be from the expected amount and still count as paid, unless the user sets another.
    /// </summary>
    public const decimal DefaultTolerancePercent = 5;

    private readonly List<BillAmount> _amounts = [];

    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private Bill()
    {
    }
#pragma warning restore CS8618

    /// <param name="userId">The user it belongs to.</param>
    /// <param name="accountId">The account it's paid from.</param>
    /// <param name="name">What the user calls it.</param>
    /// <param name="payee">Who it's paid to as the bank names them, what payments are matched on.</param>
    /// <param name="frequency">How often it's due.</param>
    /// <param name="expectedAmount">What it's expected to cost from <paramref name="startsOn"/>.</param>
    /// <param name="startsOn">Its first due date, nothing is due before.</param>
    /// <param name="source">Where it came from.</param>
    /// <param name="sourceId">The <see cref="StandingOrder"/> or <see cref="DirectDebit"/> it was seeded from, null
    /// for the other sources.</param>
    /// <exception cref="ArgumentException">The expected amount isn't more than 0, or <paramref name="sourceId"/>
    /// doesn't fit <paramref name="source"/>.</exception>
    public Bill(Guid userId, Guid accountId, string name, string payee, BillFrequency frequency,
        decimal expectedAmount, DateOnly startsOn, BillSource source, Guid? sourceId = null) : base(userId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(accountId, Guid.Empty);
        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown bill source.");
        }

        bool fromSync = source is BillSource.StandingOrder or BillSource.DirectDebit;
        if (fromSync != sourceId is not null)
        {
            throw new ArgumentException("Only a bill from a standing order or direct debit links to its source.",
                nameof(sourceId));
        }

        AccountId = accountId;
        Source = source;
        SourceId = sourceId;
        StartsOn = startsOn;
        TolerancePercent = DefaultTolerancePercent;
        Rename(name);
        ChangePayee(payee);
        Reschedule(frequency, startsOn);
        ChangeAmount(expectedAmount, startsOn);
    }

    /// <summary>
    /// What the user calls it.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// The account it's paid from.
    /// </summary>
    public Guid AccountId { get; private set; }

    /// <summary>
    /// Who it's paid to as the bank names them, what payments are matched on.
    /// </summary>
    public string Payee { get; private set; }

    /// <summary>
    /// Where it came from.
    /// </summary>
    public BillSource Source { get; private set; }

    /// <summary>
    /// The <see cref="StandingOrder"/> or <see cref="DirectDebit"/> it was seeded from, null for the other sources.
    /// A sync replaces those wholesale, so when it no longer exists the bill has ended, see <see cref="End"/>.
    /// </summary>
    public Guid? SourceId { get; private set; }

    /// <summary>
    /// How often it's due.
    /// </summary>
    public BillFrequency Frequency { get; private set; }

    /// <summary>
    /// The first due date, nothing is due before.
    /// </summary>
    public DateOnly StartsOn { get; private set; }

    /// <summary>
    /// One due date the others are counted from, every <see cref="Frequency"/> either side. Counted from each time
    /// rather than step by step, so a monthly bill due on the 31st is due on the 30th in short months and back on the
    /// 31st after.
    /// </summary>
    public DateOnly Anchor { get; private set; }

    /// <summary>
    /// The last day it could be due, null while it runs.
    /// </summary>
    public DateOnly? EndedOn { get; private set; }

    /// <summary>
    /// Whether it has ended, see <see cref="EndedOn"/>.
    /// </summary>
    public bool HasEnded => EndedOn is not null;

    /// <summary>
    /// Whether the user ended it, which a sync never undoes, see <see cref="Reopen"/>.
    /// </summary>
    public bool EndedByUser { get; private set; }

    /// <summary>
    /// How far a period's payment can be from the expected amount, as a percentage of it, and still count as paid.
    /// </summary>
    public decimal TolerancePercent { get; private set; }

    /// <summary>
    /// Its expected amounts oldest first, each applying from its day until the next.
    /// </summary>
    public IReadOnlyList<BillAmount> Amounts => _amounts.AsReadOnly();

    /// <summary>
    /// What it's expected to cost now.
    /// </summary>
    public decimal ExpectedAmount => _amounts[^1].Amount;

    /// <summary>
    /// Changes what the user calls it.
    /// </summary>
    /// <param name="name">The new name, trimmed.</param>
    [MemberNotNull(nameof(Name))]
    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    /// <summary>
    /// Changes who it's paid to. Payments are matched on this.
    /// </summary>
    /// <param name="payee">The payee as the bank names them, trimmed.</param>
    [MemberNotNull(nameof(Payee))]
    public void ChangePayee(string payee)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payee);
        Payee = payee.Trim();
    }

    /// <summary>
    /// Moves it to be paid from another account.
    /// </summary>
    /// <param name="accountId">The account it's paid from now.</param>
    public void MoveToAccount(Guid accountId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(accountId, Guid.Empty);
        AccountId = accountId;
    }

    /// <summary>
    /// Changes how often it's due, counted from <paramref name="anchor"/>. Past periods are worked out on the new
    /// schedule too, the payments that paid them stay linked.
    /// </summary>
    /// <param name="frequency">How often it's due now.</param>
    /// <param name="anchor">Any due date, e.g. the next one a standing order reports.</param>
    public void Reschedule(BillFrequency frequency, DateOnly anchor)
    {
        if (!Enum.IsDefined(frequency))
        {
            throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unknown bill frequency.");
        }

        Frequency = frequency;
        Anchor = anchor;
    }

    /// <summary>
    /// Changes how far a payment can be from the expected amount and still count as paid.
    /// </summary>
    /// <param name="percent">The tolerance as a percentage of the expected amount.</param>
    /// <exception cref="ArgumentOutOfRangeException">The percentage is negative or over 100.</exception>
    public void ChangeTolerance(decimal percent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(percent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 100);
        TolerancePercent = percent;
    }

    /// <summary>
    /// Sets the expected amount from <paramref name="from"/> onwards, replacing any set for that day or later. Periods
    /// due before keep theirs.
    /// </summary>
    /// <param name="amount">What it's expected to cost, more than 0.</param>
    /// <param name="from">The first due date it applies to.</param>
    /// <returns>Whether the expected amount changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The amount isn't more than 0.</exception>
    public bool ChangeAmount(decimal amount, DateOnly from)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        if (_amounts.Count > 0 && ExpectedOn(from) == amount && !_amounts.Exists(x => x.From > from))
        {
            return false;
        }

        _amounts.RemoveAll(x => x.From >= from);
        _amounts.Add(new BillAmount(from, amount));
        _amounts.Sort(static (left, right) => left.From.CompareTo(right.From));
        return true;
    }

    /// <summary>
    /// What a period due on <paramref name="due"/> is expected to cost. Before the first amount, the first.
    /// </summary>
    /// <param name="due">The period's due date.</param>
    public decimal ExpectedOn(DateOnly due)
    {
        BillAmount? amount = _amounts.LastOrDefault(x => x.From <= due);
        return (amount ?? _amounts[0]).Amount;
    }

    /// <summary>
    /// Whether paying <paramref name="paid"/> for the period due on <paramref name="due"/> is within
    /// <see cref="TolerancePercent"/> of what was expected.
    /// </summary>
    /// <param name="paid">What was paid.</param>
    /// <param name="due">The period's due date.</param>
    public bool IsExpected(decimal paid, DateOnly due)
    {
        decimal expected = ExpectedOn(due);
        return Math.Abs(paid - expected) <= expected * TolerancePercent / 100;
    }

    /// <summary>
    /// Follows a price change, the price having changed when <paramref name="latest"/>, its latest paid period, was
    /// paid outside the tolerance. The new amount applies from the day after that period's due date, so the period
    /// itself still shows <see cref="BillState.Changed"/> and the next one expects the new price.
    /// </summary>
    /// <param name="latest">Its latest paid period.</param>
    /// <returns>Whether the expected amount changed.</returns>
    public bool FollowPrice(BillPeriod latest)
    {
        ArgumentNullException.ThrowIfNull(latest);

        if (latest.State != BillState.Changed || latest.Paid <= 0)
        {
            return false;
        }

        return ChangeAmount(latest.Paid, latest.Due.AddDays(1));
    }

    /// <summary>
    /// Stops it being due after <paramref name="on"/>, e.g. its standing order was cancelled.
    /// </summary>
    /// <param name="on">The last day it could be due.</param>
    /// <returns>Whether it was running.</returns>
    public bool End(DateOnly on)
    {
        if (HasEnded)
        {
            return false;
        }

        EndedOn = on;
        return true;
    }

    /// <summary>
    /// The user stops it being due after <paramref name="on"/>. Unlike <see cref="End"/>, a sync won't reopen it
    /// while its standing order or direct debit still runs, only <see cref="ReopenByUser"/> does. One that had
    /// already ended keeps the day it ended.
    /// </summary>
    /// <param name="on">The last day it could be due.</param>
    /// <returns>Whether it changed.</returns>
    public bool EndByUser(DateOnly on)
    {
        if (EndedByUser)
        {
            return false;
        }

        EndedOn ??= on;
        EndedByUser = true;
        return true;
    }

    /// <summary>
    /// Makes it due again after <see cref="End"/>, e.g. its standing order restarted. Leaves one the user ended.
    /// </summary>
    /// <returns>Whether it had ended.</returns>
    public bool Reopen()
    {
        if (!HasEnded || EndedByUser)
        {
            return false;
        }

        EndedOn = null;
        return true;
    }

    /// <summary>
    /// The user makes it due again, however it ended. A sync ends it again if its source is gone.
    /// </summary>
    /// <returns>Whether it had ended.</returns>
    public bool ReopenByUser()
    {
        if (!HasEnded)
        {
            return false;
        }

        EndedOn = null;
        EndedByUser = false;
        return true;
    }

    /// <summary>
    /// Its due dates from <paramref name="from"/> to <paramref name="to"/>, both included, between
    /// <see cref="StartsOn"/> and <see cref="EndedOn"/>.
    /// </summary>
    /// <param name="from">The first day to look at.</param>
    /// <param name="to">The last day to look at.</param>
    public IEnumerable<DateOnly> DueDatesBetween(DateOnly from, DateOnly to)
    {
        for (int index = IndexOnOrBefore(from); DueAt(index) <= to; index++)
        {
            DateOnly due = DueAt(index);
            if (due >= from && due >= StartsOn && (EndedOn is null || due <= EndedOn))
            {
                yield return due;
            }
        }
    }

    /// <summary>
    /// Its first due date on or after <paramref name="day"/>, null once it has ended.
    /// </summary>
    /// <param name="day">The day to look from.</param>
    public DateOnly? NextDue(DateOnly day)
    {
        // Nothing is due less often than yearly, so a year and a bit always finds the next one
        foreach (DateOnly due in DueDatesBetween(day, day.AddYears(1).AddDays(1)))
        {
            return due;
        }

        return null;
    }

    /// <summary>
    /// The due date a payment on <paramref name="day"/> is for, the closest, the earlier one when it's halfway. Never
    /// before <see cref="StartsOn"/>.
    /// </summary>
    /// <param name="day">The day the payment was made.</param>
    public DateOnly NearestDue(DateOnly day)
    {
        int index = IndexOnOrBefore(day);
        DateOnly before = DueAt(index);
        DateOnly after = DueAt(index + 1);

        DateOnly nearest = before;
        if (after.DayNumber - day.DayNumber < day.DayNumber - before.DayNumber)
        {
            nearest = after;
        }

        if (nearest < StartsOn)
        {
            return FirstDue();
        }

        return nearest;
    }

    /// <summary>
    /// The first due date on or after <see cref="StartsOn"/>, which after <see cref="Reschedule"/> may not be the
    /// start itself.
    /// </summary>
    private DateOnly FirstDue()
    {
        int index = IndexOnOrBefore(StartsOn);
        if (DueAt(index) < StartsOn)
        {
            index++;
        }

        return DueAt(index);
    }

    /// <summary>
    /// Counts <paramref name="index"/> periods from <see cref="Anchor"/>, back when negative.
    /// </summary>
    /// <param name="index">How many periods from the anchor.</param>
    private DateOnly DueAt(int index) => Frequency switch
    {
        BillFrequency.Weekly => Anchor.AddDays(7 * index),
        BillFrequency.Fortnightly => Anchor.AddDays(14 * index),
        BillFrequency.FourWeekly => Anchor.AddDays(28 * index),
        BillFrequency.Quarterly => Anchor.AddMonths(3 * index),
        BillFrequency.Yearly => Anchor.AddYears(index),
        _ => Anchor.AddMonths(index)
    };

    /// <summary>
    /// The index of the last due date on or before <paramref name="day"/>, see <see cref="DueAt"/>.
    /// </summary>
    /// <param name="day">The day to look from.</param>
    private int IndexOnOrBefore(DateOnly day)
    {
        int index = (day.DayNumber - Anchor.DayNumber) / ApproximateDays();

        while (DueAt(index) > day)
        {
            index--;
        }

        while (DueAt(index + 1) <= day)
        {
            index++;
        }

        return index;
    }

    /// <summary>
    /// Days in a period near enough to start <see cref="IndexOnOrBefore"/> within one or two of the answer.
    /// </summary>
    private int ApproximateDays() => Frequency switch
    {
        BillFrequency.Weekly => 7,
        BillFrequency.Fortnightly => 14,
        BillFrequency.FourWeekly => 28,
        BillFrequency.Quarterly => 91,
        BillFrequency.Yearly => 365,
        _ => 30
    };
}
