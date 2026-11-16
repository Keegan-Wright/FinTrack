using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Sharing;
using DomainLink = FinanceTracker.Domain.Sharing.UserLink;

namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// Merging and splitting the joint accounts two linked people have both connected, see <see cref="SharingService"/>
/// and <see cref="JointAccounts"/>. Transfers matched again, rules run again over both copies, and
/// the other person told.
/// </summary>
public interface ISharingJointAccounts
{
    /// <inheritdoc cref="ISharingService.MergeJointAccountsAsync"/>
    Task<LinkShares> MergeAsync(Guid linkId, MergeJointAccounts request, CancellationToken cancellationToken);

    /// <inheritdoc cref="ISharingService.SplitJointAccountAsync"/>
    Task<LinkShares> SplitAsync(Guid linkId, Guid jointAccountId, CancellationToken cancellationToken);

    /// <summary>
    /// Splits every joint account merged in <paramref name="link"/>. Tracked changes, saved by the caller.
    /// </summary>
    /// <returns>Whether there were any to split.</returns>
    Task<bool> SplitAllAsync(FinanceTrackerContext context, DomainLink link, CancellationToken cancellationToken);
}
