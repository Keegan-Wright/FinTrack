namespace FinanceTracker.Domain.Bills;

/// <summary>
/// What a <see cref="Bill"/> is expected to cost from <see cref="From"/> until a later amount takes over, so a price
/// change leaves the periods before as they were.
/// </summary>
/// <param name="From">The first day it applies, compared with each period's due date.</param>
/// <param name="Amount">How much goes out each period, more than 0.</param>
public sealed record BillAmount(DateOnly From, decimal Amount);
