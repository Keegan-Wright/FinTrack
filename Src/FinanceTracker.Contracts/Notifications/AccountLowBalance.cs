namespace FinanceTracker.Contracts.Notifications;

/// <summary>
/// One of the user's accounts and how low it can go before they're told, see
/// <see cref="NotificationSettings.LowBalance"/>.
/// </summary>
public sealed class AccountLowBalance
{
    /// <summary>
    /// The account.
    /// </summary>
    public required Guid AccountId { get; init; }

    /// <summary>
    /// The account's name, e.g. <c>Everyday</c>.
    /// </summary>
    public required string AccountName { get; init; }

    /// <summary>
    /// Tell them once its available balance goes below this, the credit left for a card. Null when it isn't
    /// watched.
    /// </summary>
    public decimal? Below { get; init; }
}
