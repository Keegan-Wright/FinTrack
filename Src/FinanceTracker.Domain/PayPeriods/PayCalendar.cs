namespace FinanceTracker.Domain.PayPeriods;

/// <summary>
/// The user's pay periods, each from a pay to the day before the next, see <see cref="PayPeriod"/>. A pay that came in
/// starts its period on the day it came, otherwise on the day <see cref="PayPattern"/> says. One that's late keeps the
/// period before open until it comes, for up to <see cref="LateDays"/> days. Calendar months when there's no pay to go
/// by, see <see cref="CalendarMonths"/>.
/// </summary>
public sealed class PayCalendar
{
    /// <summary>
    /// How many days a pay can be from the day it's due and still be the pay for that month, either way.
    /// </summary>
    public const int MatchDays = 7;

    /// <summary>
    /// How long a late pay keeps the period before open. Past that the pay is taken to have stopped and the periods
    /// go by the pattern.
    /// </summary>
    public const int LateDays = 7;

    /// <summary>
    /// A pay on or after this day of the month starts a period covering more of the next month, so it's named after
    /// that.
    /// </summary>
    private const int NamedAfterNextMonthFromDay = 17;

    private readonly PayPattern? _pattern;
    private readonly SortedSet<DateOnly> _payDays;
    private readonly DateOnly _today;

    /// <param name="pattern">When pay comes in.</param>
    /// <param name="payDays">The days pay came in.</param>
    /// <param name="today">Today where the user is, so a pay not in yet can be told from a late one.</param>
    public PayCalendar(PayPattern pattern, IEnumerable<DateOnly> payDays, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(payDays);

        _pattern = pattern;
        _payDays = new SortedSet<DateOnly>(payDays);
        _today = today;
    }

    private PayCalendar()
    {
        _payDays = [];
    }

    /// <summary>
    /// Calendar months, for a user with no pay to go by or who turned pay periods off.
    /// </summary>
    public static PayCalendar CalendarMonths { get; } = new();

    /// <summary>
    /// Whether the periods run from pay to pay rather than by calendar month.
    /// </summary>
    public bool FollowsPay => _pattern is not null;

    /// <summary>
    /// The period named after <paramref name="month"/> of <paramref name="year"/>.
    /// </summary>
    public PayPeriod Named(int year, int month)
    {
        DateOnly named = new(year, month, 1);
        if (NamedAfterNextMonth())
        {
            DateOnly paidIn = named.AddMonths(-1);
            return From(paidIn.Year, paidIn.Month);
        }

        return From(year, month);
    }

    /// <summary>
    /// The period <paramref name="day"/> falls in.
    /// </summary>
    public PayPeriod Containing(DateOnly day)
    {
        // A month's pay can come in the month before, e.g. pay due on Sunday the 1st
        DateOnly month = new(day.Year, day.Month, 1);
        foreach (DateOnly paidFor in new[] { month.AddMonths(1), month, month.AddMonths(-1) })
        {
            if (StartFor(paidFor.Year, paidFor.Month) <= day)
            {
                return From(paidFor.Year, paidFor.Month);
            }
        }

        DateOnly before = month.AddMonths(-2);
        return From(before.Year, before.Month);
    }

    /// <summary>
    /// The period starting with the pay for <paramref name="month"/> of <paramref name="year"/>.
    /// </summary>
    private PayPeriod From(int year, int month)
    {
        DateOnly paidFor = new(year, month, 1);
        DateOnly next = paidFor.AddMonths(1);
        DateOnly start = StartFor(year, month);
        DateOnly end = StartFor(next.Year, next.Month).AddDays(-1);

        if (NamedAfterNextMonth())
        {
            return new PayPeriod(start, end, next.Year, next.Month);
        }

        return new PayPeriod(start, end, year, month);
    }

    /// <summary>
    /// The day the period starting with the pay for the month begins the day it came in, otherwise the day it's due,
    /// or tomorrow while it's late.
    /// </summary>
    private DateOnly StartFor(int year, int month)
    {
        if (_pattern is null)
        {
            return new DateOnly(year, month, 1);
        }

        DateOnly due = _pattern.ExpectedFor(year, month);
        if (PaidNear(due) is { } paidOn)
        {
            return paidOn;
        }

        bool late = due <= _today && _today < due.AddDays(LateDays);
        if (late)
        {
            return _today.AddDays(1);
        }

        return due;
    }

    /// <summary>
    /// The pay closest to <paramref name="due"/>, null when none came within <see cref="MatchDays"/> of it.
    /// </summary>
    private DateOnly? PaidNear(DateOnly due)
    {
        DateOnly? closest = null;
        int closestOffBy = MatchDays + 1;

        foreach (DateOnly paidOn in _payDays.GetViewBetween(due.AddDays(-MatchDays), due.AddDays(MatchDays)))
        {
            int offBy = Math.Abs(paidOn.DayNumber - due.DayNumber);
            if (offBy < closestOffBy)
            {
                closest = paidOn;
                closestOffBy = offBy;
            }
        }

        return closest;
    }

    private bool NamedAfterNextMonth()
    {
        if (_pattern is null)
        {
            return false;
        }

        return _pattern.Rule == PayDayRule.LastWorkingDay || _pattern.Day >= NamedAfterNextMonthFromDay;
    }
}
