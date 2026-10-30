namespace FinanceTracker.Domain.Bills;

/// <summary>
/// Where a <see cref="Bill"/> came from.
/// </summary>
public enum BillSource
{
    /// <summary>
    /// Seeded from a synced standing order, see <see cref="Bill.SourceId"/>.
    /// </summary>
    StandingOrder = 1,

    /// <summary>
    /// Seeded from a synced direct debit, see <see cref="Bill.SourceId"/>.
    /// </summary>
    DirectDebit = 2,

    /// <summary>
    /// Suggested from repeat payments and confirmed by the user, e.g. a card subscription.
    /// </summary>
    Detected = 3,

    /// <summary>
    /// Added by the user.
    /// </summary>
    Manual = 4
}
