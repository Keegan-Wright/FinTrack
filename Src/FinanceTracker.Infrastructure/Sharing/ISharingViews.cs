using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Sharing;

namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// Whether the user's money pages include what's shared with them, Everyone or Just mine, see
/// <see cref="SharingService"/>.
/// </summary>
public interface ISharingViews
{
    /// <inheritdoc cref="ISharingService.GetViewAsync"/>
    Task<SharingView> GetViewAsync(CancellationToken cancellationToken);

    /// <inheritdoc cref="ISharingService.ChangeViewAsync"/>
    Task<SharingView> ChangeViewAsync(ChangeSharingView request, CancellationToken cancellationToken);
}
