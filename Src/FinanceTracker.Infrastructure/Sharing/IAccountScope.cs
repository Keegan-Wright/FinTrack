namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// Which accounts the signed in user sees in the money views, and which they can change, their own and those shared
/// with them through accepted links.
/// </summary>
public interface IAccountScope
{
    /// <summary>
    /// What the money views show: their own accounts, and those shared with them unless they chose Just mine.
    /// </summary>
    Task<AccountsInView> ResolveAsync(FinanceTrackerContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Their own accounts and every account shared with them, whichever view they chose. For what's theirs wherever
    /// it's paid from, such as their bills' payments.
    /// </summary>
    Task<AccountsInView> ResolveAllAsync(FinanceTrackerContext context, CancellationToken cancellationToken);

    /// <summary>
    /// The accounts whose transactions they can change: their own, and those shared with them with changes allowed,
    /// whichever view they chose.
    /// </summary>
    Task<AccountsInView> ResolveChangeableAsync(FinanceTrackerContext context, CancellationToken cancellationToken);
}
