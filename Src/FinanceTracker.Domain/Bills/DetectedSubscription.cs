namespace FinanceTracker.Domain.Bills;

/// <summary>
/// Payments that repeat like a subscription, from <see cref="SubscriptionDetector.Detect"/>.
/// </summary>
/// <param name="Key">The payee in upper case letters only, what tells one subscription from another on an
/// account.</param>
/// <param name="Name">The payee as the bank names them, reference numbers left out.</param>
/// <param name="Amount">The latest payment's amount, more than 0.</param>
/// <param name="PaymentIds">The payments that repeat, oldest first.</param>
public sealed record DetectedSubscription(
    Guid AccountId,
    string Key,
    string Name,
    BillFrequency Frequency,
    decimal Amount,
    DateOnly FirstPaidOn,
    DateOnly LastPaidOn,
    IReadOnlyList<Guid> PaymentIds);
