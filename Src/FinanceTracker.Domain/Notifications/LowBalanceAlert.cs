namespace FinanceTracker.Domain.Notifications;

/// <summary>
/// When to tell the user an account is running low, kept on their <see cref="NotificationSettings"/>.
/// </summary>
/// <param name="AccountId">The account watched.</param>
/// <param name="Below">Tell them once its available balance goes below this. For a card that's the credit left.</param>
/// <param name="LowSince">When it went below, null while it's at or above. They're told as it goes below, once until
/// it's back above.</param>
public sealed record LowBalanceAlert(Guid AccountId, decimal Below, DateTime? LowSince = null);
