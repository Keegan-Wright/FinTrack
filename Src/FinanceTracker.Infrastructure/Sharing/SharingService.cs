using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using ContractLink = FinanceTracker.Contracts.Sharing.UserLink;

namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// Sharing between linked users, one service to the app over the parts that do the work: the links themselves
/// (<see cref="SharingLinks"/>), what each shares (<see cref="SharingChoices"/>), their joint accounts
/// (<see cref="SharingJointAccounts"/>), the Everyone or Just mine view (<see cref="SharingViews"/>), and what follows
/// a change (<see cref="SharingEffects"/>).
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ISharingService>]
public sealed class SharingService : ISharingService
{
    private readonly ISharingLinks _sharingLinks;
    private readonly ISharingChoices _sharingChoices;
    private readonly ISharingJointAccounts _sharingJointAccounts;
    private readonly ISharingViews _sharingViews;

    /// <param name="sharingLinks">Gives the user's links.</param>
    /// <param name="sharingChoices">Gives what the user may share.</param>
    /// <param name="sharingJointAccounts">Merges and splits joint accounts.</param>
    /// <param name="sharingViews">Gives what's shared, as each page shows it.</param>
    public SharingService(ISharingLinks sharingLinks, ISharingChoices sharingChoices,
        ISharingJointAccounts sharingJointAccounts, ISharingViews sharingViews)
    {
        _sharingLinks = sharingLinks;
        _sharingChoices = sharingChoices;
        _sharingJointAccounts = sharingJointAccounts;
        _sharingViews = sharingViews;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<ContractLink> GetLinksAsync(CancellationToken cancellationToken) =>
        _sharingLinks.GetLinksAsync(cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<ContractLink> GetPeopleAsync(CancellationToken cancellationToken) =>
        _sharingLinks.GetPeopleAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<InviteOutcome> InviteAsync(InviteToShare request, CancellationToken cancellationToken) =>
        _sharingLinks.InviteAsync(request, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> AcceptAsync(Guid id, CancellationToken cancellationToken) =>
        _sharingLinks.AcceptAsync(id, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> DeclineAsync(Guid id, CancellationToken cancellationToken) =>
        _sharingLinks.DeclineAsync(id, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> EndAsync(Guid id, CancellationToken cancellationToken) =>
        _sharingLinks.EndAsync(id, cancellationToken);

    /// <inheritdoc/>
    public Task<LinkShares?> GetSharesAsync(Guid linkId, CancellationToken cancellationToken) =>
        _sharingChoices.GetSharesAsync(linkId, cancellationToken);

    /// <inheritdoc/>
    public Task<LinkShares> ChangeAccountSharesAsync(Guid linkId, ChangeAccountShares request,
        CancellationToken cancellationToken) =>
        _sharingChoices.ChangeAccountSharesAsync(linkId, request, cancellationToken);

    /// <inheritdoc/>
    public Task<LinkShares> ChangeItemSharesAsync(Guid linkId, ChangeItemShares request,
        CancellationToken cancellationToken) =>
        _sharingChoices.ChangeItemSharesAsync(linkId, request, cancellationToken);

    /// <inheritdoc/>
    public Task<LinkShares> MergeJointAccountsAsync(Guid linkId, MergeJointAccounts request,
        CancellationToken cancellationToken) =>
        _sharingJointAccounts.MergeAsync(linkId, request, cancellationToken);

    /// <inheritdoc/>
    public Task<LinkShares> SplitJointAccountAsync(Guid linkId, Guid jointAccountId,
        CancellationToken cancellationToken) =>
        _sharingJointAccounts.SplitAsync(linkId, jointAccountId, cancellationToken);

    /// <inheritdoc/>
    public Task<SharingView> GetViewAsync(CancellationToken cancellationToken) =>
        _sharingViews.GetViewAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<SharingView> ChangeViewAsync(ChangeSharingView request, CancellationToken cancellationToken) =>
        _sharingViews.ChangeViewAsync(request, cancellationToken);
}
