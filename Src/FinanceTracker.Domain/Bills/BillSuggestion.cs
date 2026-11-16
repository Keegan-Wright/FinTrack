using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Bills;

/// <summary>
/// Repeat payments that look like a subscription, suggested to the user as a bill. Never a bill until they confirm
/// it, see <see cref="ToBill"/>. Kept once dismissed, so the same payments aren't suggested again.
/// </summary>
public sealed class BillSuggestion : AggregateRoot
{
    private readonly List<Guid> _paymentIds = [];

    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private BillSuggestion()
    {
    }
#pragma warning restore CS8618

    public BillSuggestion(Guid userId, DetectedSubscription detected) : base(userId)
    {
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentOutOfRangeException.ThrowIfEqual(detected.AccountId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(detected.Key);

        AccountId = detected.AccountId;
        Key = detected.Key;
        Refresh(detected);
    }

    /// <summary>
    /// The account the payments come out of.
    /// </summary>
    public Guid AccountId { get; private set; }

    /// <summary>
    /// The payee in upper case letters only, which with <see cref="AccountId"/> says which payments it's about.
    /// </summary>
    public string Key { get; private set; }

    /// <summary>
    /// The payee as the bank names them, the bill's name and payee if confirmed.
    /// </summary>
    public string Name { get; private set; }

    public BillFrequency Frequency { get; private set; }

    /// <summary>
    /// The latest payment's amount.
    /// </summary>
    public decimal Amount { get; private set; }

    public DateOnly FirstPaidOn { get; private set; }

    public DateOnly LastPaidOn { get; private set; }

    /// <summary>
    /// The payments that repeat, oldest first, shown so the user can see why it's suggested.
    /// </summary>
    public IReadOnlyList<Guid> PaymentIds => _paymentIds.AsReadOnly();

    /// <summary>
    /// Whether the user said it isn't a bill. Kept so it isn't suggested again.
    /// </summary>
    public bool IsDismissed { get; private set; }

    /// <summary>
    /// Whether <paramref name="detected"/> is about the same payments, the same payee on the same account.
    /// </summary>
    public bool IsFor(DetectedSubscription detected)
    {
        ArgumentNullException.ThrowIfNull(detected);
        return AccountId == detected.AccountId && Key == detected.Key;
    }

    /// <summary>
    /// Takes in the latest payments.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="detected"/> is about other payments, see
    /// <see cref="IsFor"/>.</exception>
    [MemberNotNull(nameof(Name))]
    public void Refresh(DetectedSubscription detected)
    {
        ArgumentNullException.ThrowIfNull(detected);
        if (detected.AccountId != AccountId || detected.Key != Key)
        {
            throw new ArgumentException("A suggestion can only take in its own payments.", nameof(detected));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(detected.Name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(detected.Amount);

        Name = detected.Name;
        Frequency = detected.Frequency;
        Amount = detected.Amount;
        FirstPaidOn = detected.FirstPaidOn;
        LastPaidOn = detected.LastPaidOn;
        _paymentIds.Clear();
        _paymentIds.AddRange(detected.PaymentIds);
    }

    public void Dismiss() => IsDismissed = true;

    /// <summary>
    /// The bill the user confirms it as, due from its first payment on the schedule of its latest.
    /// </summary>
    /// <exception cref="InvalidOperationException">It was dismissed.</exception>
    public Bill ToBill()
    {
        if (IsDismissed)
        {
            throw new DomainRuleException("A dismissed suggestion can't become a bill.");
        }

        Bill bill = new(UserId, AccountId, Name, Name, Frequency, Amount, FirstPaidOn, BillSource.Detected);
        bill.Reschedule(Frequency, LastPaidOn);
        return bill;
    }
}
