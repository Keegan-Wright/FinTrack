using FinanceTracker.Contracts.Sharing;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The other users the signed in user shares with inviting them, answering their invites, and ending links.
/// </summary>
public interface ISharingService
{
    /// <summary>
    /// Everyone they're linked with, and invites waiting for an answer either way. Declined and ended links aren't
    /// included.
    /// </summary>
    IAsyncEnumerable<UserLink> GetLinksAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Invites another user to share, and tells them. Validate the request with
    /// <see cref="Validation.InviteToShareValidator"/> first.
    /// </summary>
    Task<InviteOutcome> InviteAsync(InviteToShare request, CancellationToken cancellationToken);

    /// <summary>
    /// Accepts an invite sent to the user, and tells whoever sent it.
    /// </summary>
    /// <returns><see langword="false"/> if there's no invite with <paramref name="id"/> waiting for them.</returns>
    Task<bool> AcceptAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Declines an invite sent to the user.
    /// </summary>
    /// <returns><see langword="false"/> if there's no invite with <paramref name="id"/> waiting for them.</returns>
    Task<bool> DeclineAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Withdraws an invite the user sent, or ends a link either of them made. Whatever they shared stops being
    /// shared.
    /// </summary>
    /// <returns><see langword="false"/> if the user has no live link or invite of their own with
    /// <paramref name="id"/>.</returns>
    Task<bool> EndAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// What's shared both ways in one of the user's accepted links.
    /// </summary>
    /// <returns>Null if they have no accepted link with <paramref name="linkId"/>.</returns>
    Task<LinkShares?> GetSharesAsync(Guid linkId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces which of the user's accounts they share in one of their accepted links, and how. Validate the request
    /// with <see cref="Validation.ChangeAccountSharesValidator"/> first.
    /// </summary>
    /// <returns>What's shared both ways now.</returns>
    /// <exception cref="InvalidOperationException">They have no accepted link with <paramref name="linkId"/>, or
    /// an account in the request isn't theirs.</exception>
    Task<LinkShares> ChangeAccountSharesAsync(Guid linkId, ChangeAccountShares request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether the money views show accounts shared with the user, and whether any are.
    /// </summary>
    Task<SharingView> GetViewAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Chooses Everyone or Just mine for the money views, kept for the user wherever they sign in.
    /// </summary>
    Task<SharingView> ChangeViewAsync(ChangeSharingView request, CancellationToken cancellationToken);
}
