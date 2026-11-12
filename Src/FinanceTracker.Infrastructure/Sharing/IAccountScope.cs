namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// Which accounts the signed in user sees in the money views, for the read paths that show accounts, transactions
/// and balances. Changes stay with each user's own, see <see cref="FinanceTrackerContextExtensions"/>.
/// </summary>
public interface IAccountScope
{
    /// <summary>
    /// Their own accounts, and the accounts shared with them through accepted links unless they chose Just mine.
    /// </summary>
    Task<AccountsInView> ResolveAsync(FinanceTrackerContext context, CancellationToken cancellationToken);
}
