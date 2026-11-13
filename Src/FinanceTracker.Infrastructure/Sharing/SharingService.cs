using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    private readonly SharingLinks _links;
    private readonly SharingChoices _choices;
    private readonly SharingJointAccounts _jointAccounts;
    private readonly SharingViews _views;

    public SharingService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<SharingService> logger,
        IServiceScopeFactory scopeFactory, TimeProvider timeProvider)
    {
        SharingEffects effects = new(currentUser, financeTrackerContextFactory, logger, scopeFactory);
        _choices = new SharingChoices(currentUser, financeTrackerContextFactory, logger, timeProvider, effects);
        _jointAccounts = new SharingJointAccounts(currentUser, financeTrackerContextFactory, logger, timeProvider,
            effects, _choices);
        _links = new SharingLinks(currentUser, financeTrackerContextFactory, logger, timeProvider, effects,
            _jointAccounts);
        _views = new SharingViews(currentUser, financeTrackerContextFactory, logger);
    }

    public IAsyncEnumerable<ContractLink> GetLinksAsync(CancellationToken cancellationToken) =>
        _links.GetLinksAsync(cancellationToken);

    public IAsyncEnumerable<ContractLink> GetPeopleAsync(CancellationToken cancellationToken) =>
        _links.GetPeopleAsync(cancellationToken);

    public Task<InviteOutcome> InviteAsync(InviteToShare request, CancellationToken cancellationToken) =>
        _links.InviteAsync(request, cancellationToken);

    public Task<bool> AcceptAsync(Guid id, CancellationToken cancellationToken) =>
        _links.AcceptAsync(id, cancellationToken);

    public Task<bool> DeclineAsync(Guid id, CancellationToken cancellationToken) =>
        _links.DeclineAsync(id, cancellationToken);

    public Task<bool> EndAsync(Guid id, CancellationToken cancellationToken) =>
        _links.EndAsync(id, cancellationToken);

    public Task<LinkShares?> GetSharesAsync(Guid linkId, CancellationToken cancellationToken) =>
        _choices.GetSharesAsync(linkId, cancellationToken);

    public Task<LinkShares> ChangeAccountSharesAsync(Guid linkId, ChangeAccountShares request,
        CancellationToken cancellationToken) =>
        _choices.ChangeAccountSharesAsync(linkId, request, cancellationToken);

    public Task<LinkShares> ChangeItemSharesAsync(Guid linkId, ChangeItemShares request,
        CancellationToken cancellationToken) =>
        _choices.ChangeItemSharesAsync(linkId, request, cancellationToken);

    public Task<LinkShares> MergeJointAccountsAsync(Guid linkId, MergeJointAccounts request,
        CancellationToken cancellationToken) =>
        _jointAccounts.MergeAsync(linkId, request, cancellationToken);

    public Task<LinkShares> SplitJointAccountAsync(Guid linkId, Guid jointAccountId,
        CancellationToken cancellationToken) =>
        _jointAccounts.SplitAsync(linkId, jointAccountId, cancellationToken);

    public Task<SharingView> GetViewAsync(CancellationToken cancellationToken) =>
        _views.GetViewAsync(cancellationToken);

    public Task<SharingView> ChangeViewAsync(ChangeSharingView request, CancellationToken cancellationToken) =>
        _views.ChangeViewAsync(request, cancellationToken);
}
