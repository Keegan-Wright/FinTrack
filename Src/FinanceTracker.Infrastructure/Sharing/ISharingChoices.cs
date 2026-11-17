using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Sharing;
using DomainLink = FinanceTracker.Domain.Sharing.UserLink;

namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// What each person shares in a link, see <see cref="SharingService"/>. Reading it both ways, and changing the accounts
/// and other things the user shares.
/// </summary>
public interface ISharingChoices
{
    /// <inheritdoc cref="ISharingService.GetSharesAsync"/>
    Task<LinkShares?> GetSharesAsync(Guid linkId, CancellationToken cancellationToken);

    /// <inheritdoc cref="ISharingService.ChangeAccountSharesAsync"/>
    Task<LinkShares> ChangeAccountSharesAsync(Guid linkId, ChangeAccountShares request,
        CancellationToken cancellationToken);

    /// <inheritdoc cref="ISharingService.ChangeItemSharesAsync"/>
    Task<LinkShares> ChangeItemSharesAsync(Guid linkId, ChangeItemShares request,
        CancellationToken cancellationToken);

    /// <summary>
    /// What's shared both ways in <paramref name="link"/>. Every account of the user's with how it's shared, and the
    /// accounts the other person shares.
    /// </summary>
    /// <param name="context">The context to work through.</param>
    /// <param name="link">The link between the two people.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    Task<LinkShares> LinkSharesAsync(FinanceTrackerContext context, DomainLink link,
        CancellationToken cancellationToken);
}
