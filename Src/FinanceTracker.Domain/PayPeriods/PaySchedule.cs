using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.PayPeriods;

/// <summary>
/// The user's pay, picked up from their transactions, see <see cref="PayDetector"/>, and the days it came in, which
/// their pay periods run between, see <see cref="Calendar"/>. One per user. Keeps every pay day it has seen, so past
/// periods stay as they were once the transactions are older than a sync looks at.
/// </summary>
public sealed class PaySchedule : AggregateRoot
{
    private readonly List<DateOnly> _payDays = [];

    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private PaySchedule()
    {
    }
#pragma warning restore CS8618

    public PaySchedule(Guid userId, DetectedPay pay) : base(userId)
    {
        Follow(pay, chosen: false);
    }

    /// <summary>
    /// The account it's paid into.
    /// </summary>
    public Guid AccountId { get; private set; }

    /// <summary>
    /// The payer in upper case letters only, which with <see cref="AccountId"/> says which payments are the pay.
    /// </summary>
    public string Key { get; private set; }

    /// <summary>
    /// The payer as the bank names them.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// The latest pay.
    /// </summary>
    public decimal Amount { get; private set; }

    public PayDayRule Rule { get; private set; }

    /// <summary>
    /// The day of the month for <see cref="PayDayRule.DayOfMonth"/>, see <see cref="PayPattern.Day"/>.
    /// </summary>
    public int Day { get; private set; }

    public PayPattern Pattern => new(Rule, Day);

    /// <summary>
    /// The days the pay came in where the user is, oldest first.
    /// </summary>
    public IReadOnlyCollection<DateOnly> PayDays => _payDays.AsReadOnly();

    /// <summary>
    /// Whether the user chose this income, so it's kept even when another comes in like pay.
    /// </summary>
    public bool IsChosen { get; private set; }

    /// <summary>
    /// Whether the user turned pay periods off, back to calendar months. It's still followed, so turning them back on
    /// has every pay.
    /// </summary>
    public bool IsOff { get; private set; }

    /// <summary>
    /// The user's periods, from pay to pay, or calendar months while turned off.
    /// </summary>
    /// <param name="today">Today where the user is.</param>
    public PayCalendar Calendar(DateOnly today)
    {
        if (IsOff)
        {
            return PayCalendar.CalendarMonths;
        }

        return new PayCalendar(Pattern, _payDays, today);
    }

    public bool IsFor(DetectedPay pay)
    {
        ArgumentNullException.ThrowIfNull(pay);
        return AccountId == pay.AccountId && Key == pay.Key;
    }

    /// <summary>
    /// Takes in what a sync found the latest pays while this pay is among them, otherwise the largest found when the
    /// user didn't choose this one. A chosen pay that wasn't found is kept, its pattern says when the next is due.
    /// </summary>
    /// <param name="detected">What <see cref="PayDetector.Detect"/> found, largest first.</param>
    public void Update(IReadOnlyList<DetectedPay> detected)
    {
        ArgumentNullException.ThrowIfNull(detected);

        DetectedPay? same = detected.FirstOrDefault(IsFor);
        if (same is not null)
        {
            Follow(same, IsChosen);
            return;
        }

        if (!IsChosen && detected.Count > 0)
        {
            Follow(detected[0], chosen: false);
        }
    }

    /// <summary>
    /// The user said <paramref name="pay"/> is their pay, which turns pay periods back on.
    /// </summary>
    public void Choose(DetectedPay pay)
    {
        Follow(pay, chosen: true);
        IsOff = false;
    }

    public void TurnOff() => IsOff = true;

    public void TurnOn() => IsOff = false;

    /// <summary>
    /// Follows <paramref name="pay"/>, adding its pay days to those seen, or starting again from them when it's
    /// another payer.
    /// </summary>
    [MemberNotNull(nameof(Key), nameof(Name))]
    private void Follow(DetectedPay pay, bool chosen)
    {
        ArgumentNullException.ThrowIfNull(pay);
        ArgumentOutOfRangeException.ThrowIfEqual(pay.AccountId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(pay.Key);

        if (!IsFor(pay))
        {
            _payDays.Clear();
        }

        AccountId = pay.AccountId;
        Key = pay.Key;
        Name = pay.Name;
        Amount = pay.Amount;
        Rule = pay.Pattern.Rule;
        Day = pay.Pattern.Day;
        IsChosen = chosen;

        foreach (DateOnly payDay in pay.PayDays.Where(x => !_payDays.Contains(x)))
        {
            _payDays.Add(payDay);
        }

        _payDays.Sort();
    }
}
