using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Sharing;
using ContractLink = FinanceTracker.Contracts.Sharing.UserLink;

namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// The links between people, see <see cref="SharingService"/>. Listing them, inviting, answering and ending. Ending
/// splits their joint accounts and takes off everything shared, see <see cref="EndAsync"/>.
/// </summary>
public interface ISharingLinks
{
    /// <inheritdoc cref="ISharingService.GetLinksAsync"/>
    IAsyncEnumerable<ContractLink> GetLinksAsync(CancellationToken cancellationToken);

    /// <inheritdoc cref="ISharingService.GetPeopleAsync"/>
    IAsyncEnumerable<ContractLink> GetPeopleAsync(CancellationToken cancellationToken);

    /// <inheritdoc cref="ISharingService.InviteAsync"/>
    Task<InviteOutcome> InviteAsync(InviteToShare request, CancellationToken cancellationToken);

    /// <inheritdoc cref="ISharingService.AcceptAsync"/>
    Task<bool> AcceptAsync(Guid id, CancellationToken cancellationToken);

    /// <inheritdoc cref="ISharingService.DeclineAsync"/>
    Task<bool> DeclineAsync(Guid id, CancellationToken cancellationToken);

    /// <inheritdoc cref="ISharingService.EndAsync"/>
    Task<bool> EndAsync(Guid id, CancellationToken cancellationToken);
}
