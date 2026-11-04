namespace FinanceTracker.Domain.PayPeriods;

/// <summary>
/// Money that comes in like pay, from <see cref="PayDetector.Detect"/>.
/// </summary>
/// <param name="AccountId">The account it's paid into.</param>
/// <param name="Key">The payer in upper case letters only, which with <paramref name="AccountId"/> says which
/// payments it is.</param>
/// <param name="Name">The payer as the bank names them.</param>
/// <param name="Amount">The latest pay.</param>
/// <param name="Pattern">When it comes in, the latest pay's day of the month when no pattern fits them all.</param>
/// <param name="PayDays">The days it came in where the user is, oldest first.</param>
public sealed record DetectedPay(Guid AccountId, string Key, string Name, decimal Amount, PayPattern Pattern,
    IReadOnlyList<DateOnly> PayDays);
