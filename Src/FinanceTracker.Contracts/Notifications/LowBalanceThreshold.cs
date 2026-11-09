namespace FinanceTracker.Contracts.Notifications;

/// <summary>
/// How low one account can go before the user is told, see <see cref="ChangeAlertSettings"/>.
/// </summary>
public sealed class LowBalanceThreshold
{
    /// <summary>
    /// The account, one of the user's.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Notify them once its available balance goes below this, the credit left for a card.
    /// </summary>
    public decimal Below { get; set; }
}
