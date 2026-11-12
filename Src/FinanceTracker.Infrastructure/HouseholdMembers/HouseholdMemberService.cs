using System.Runtime.CompilerServices;
using FinanceTracker.Contracts;
using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using Microsoft.Extensions.Logging;
using HouseholdMember = FinanceTracker.Contracts.HouseholdMember.HouseholdMember;

namespace FinanceTracker.Infrastructure.HouseholdMembers;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IHouseholdMemberService>]
public class HouseholdMemberService : ServiceBase<HouseholdMemberService>, IHouseholdMemberService
{
    private readonly ITransferMatchingService _transferMatchingService;

    public HouseholdMemberService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<HouseholdMemberService> logger,
        ITransferMatchingService transferMatchingService) : base(currentUser, financeTrackerContextFactory, logger)
    {
        _transferMatchingService = transferMatchingService;
    }

    public async IAsyncEnumerable<HouseholdMember> GetHouseholdMembersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        List<Domain.Household.HouseholdMember> members = await context.HouseholdMembers.OwnedBy(UserId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        Dictionary<Guid, string> usernames = await UsernamesAsync(context, members, cancellationToken);
        foreach (Domain.Household.HouseholdMember householdMember in members)
        {
            yield return ToContract(householdMember, usernames);
        }
    }

    public async Task<HouseholdMember> AddHouseholdMemberAsync(AddHouseholdMember categoryToAdd,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureNameIsFreeAsync(context, categoryToAdd.FirstName, categoryToAdd.LastName, null,
            cancellationToken);
        Domain.Household.HouseholdMember householdMember = new(UserId, categoryToAdd.FirstName,
            categoryToAdd.LastName, categoryToAdd.YearlyIncome);

        context.HouseholdMembers.Add(householdMember);
        await NameCheck.SaveKeepingNamesApartAsync(context, NameTakenException.Person,
            FullName(categoryToAdd.FirstName, categoryToAdd.LastName), cancellationToken);
        Logger.LogChanged(UserId, "created", "household member", householdMember.Id);

        return ToContract(householdMember, await UsernamesAsync(context, [householdMember], cancellationToken));
    }

    public async Task<HouseholdMember> UpdateHouseholdMemberAsync(Guid id, UpdateHouseholdMember request,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        Domain.Household.HouseholdMember householdMember =
            await context.HouseholdMembers.OwnedBy(UserId).SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Household member {id} was not found.");

        await EnsureNameIsFreeAsync(context, request.FirstName, request.LastName, id, cancellationToken);
        householdMember.Rename(request.FirstName, request.LastName);
        householdMember.ChangeYearlyIncome(request.YearlyIncome);
        await NameCheck.SaveKeepingNamesApartAsync(context, NameTakenException.Person,
            FullName(request.FirstName, request.LastName), cancellationToken);
        Logger.LogChanged(UserId, "updated", "household member", id);

        return ToContract(householdMember, await UsernamesAsync(context, [householdMember], cancellationToken));
    }

    public async Task<HouseholdMemberRecognitionSaved> UpdateRecognitionAsync(Guid id,
        UpdateHouseholdMemberRecognition request, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        Domain.Household.HouseholdMember householdMember =
            await context.HouseholdMembers.OwnedBy(UserId).SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Household member {id} was not found.");

        householdMember.ReplaceRecognition(request.PayeeNames, request.LinkedAccounts.Select(static x =>
            AccountIdentifier.Create(x.SortCode, x.Number, null)
            ?? throw new ArgumentException("A linked account needs a sort code and number.", nameof(request))));

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "changed how transfers are recognised for", "household member", id);

        // Applied to the transactions already there now, rather than waiting for the next sync
        await _transferMatchingService.MatchAsync(cancellationToken);

        int recognised = await context.BankTransactions.OwnedBy(UserId)
            .CountAsync(x => x.Transfer != null && x.Transfer.Decision == TransferDecision.Transfer &&
                             x.Transfer.PartnerHouseholdMemberId == id, cancellationToken);

        return new HouseholdMemberRecognitionSaved
        {
            Member = ToContract(householdMember, await UsernamesAsync(context, [householdMember], cancellationToken)),
            RecognisedTransactions = recognised
        };
    }

    public async Task<bool> DeleteHouseholdMemberAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        Domain.Household.HouseholdMember? householdMember = await context.HouseholdMembers.OwnedBy(UserId)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (householdMember == null)
        {
            return false;
        }

        context.HouseholdMembers.Remove(householdMember);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "deleted", "household member", id);

        // Their transactions were transfers because of them, so count again or go back to review
        await _transferMatchingService.MatchAsync(cancellationToken);
        return true;

    }

    /// <exception cref="NameTakenException">Someone else in the household has the same first and last name.
    /// </exception>
    private async Task EnsureNameIsFreeAsync(FinanceTrackerContext context, string firstName, string lastName,
        Guid? exceptId, CancellationToken cancellationToken)
    {
        List<Domain.Household.HouseholdMember> existing = await context.HouseholdMembers.OwnedBy(UserId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        NameCheck.EnsureFree(existing.Select(static x => (x.Id, FullName(x.FirstName, x.LastName))),
            FullName(firstName, lastName), exceptId, NameTakenException.Person);
    }

    private static string FullName(string firstName, string lastName) => $"{firstName.Trim()} {lastName.Trim()}";

    public async Task<HouseholdMember> LinkHouseholdMemberAsync(Guid id, LinkHouseholdMember request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        Domain.Household.HouseholdMember householdMember =
            await context.HouseholdMembers.OwnedBy(UserId).SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Household member {id} was not found.");

        if (request.LinkId is not { } linkId)
        {
            householdMember.UnlinkUser();
            await context.SaveChangesAsync(cancellationToken);
            Logger.LogChanged(UserId, "unlinked", "household member", id);
            return ToContract(householdMember, []);
        }

        // Someone they're linked with, taking their name
        Guid userId = UserId;
        Domain.Sharing.UserLink link = await context.UserLinks
                                           .SingleOrDefaultAsync(x => x.Id == linkId &&
                                                                      x.Status == UserLinkStatus.Accepted &&
                                                                      (x.UserId == userId || x.InviteeId == userId),
                                               cancellationToken)
                                       ?? throw new InvalidOperationException($"No accepted link {linkId} was found.");
        Guid personId = link.OtherThan(userId);
        FinanceTrackerUser person = await context.Users.AsNoTracking().SingleAsync(x => x.Id == personId,
            cancellationToken);

        bool alreadyAnother = await context.HouseholdMembers.OwnedBy(userId)
            .AnyAsync(x => x.LinkedUserId == personId && x.Id != id, cancellationToken);

        if (alreadyAnother)
        {
            throw new InvalidOperationException("Another household member is already that person.");
        }

        string firstName = person.FirstName ?? person.UserName ?? string.Empty;
        string lastName = person.LastName ?? string.Empty;
        await EnsureNameIsFreeAsync(context, firstName, lastName, id, cancellationToken);
        householdMember.LinkToUser(personId, firstName, lastName);

        await NameCheck.SaveKeepingNamesApartAsync(context, NameTakenException.Person, FullName(firstName, lastName),
            cancellationToken);
        Logger.LogChanged(UserId, "linked", "household member", id);

        return ToContract(householdMember,
            new Dictionary<Guid, string> { [personId] = person.UserName ?? string.Empty });
    }

    /// <summary>
    /// The usernames of the users <paramref name="members"/> are linked to, by user.
    /// </summary>
    private static async Task<Dictionary<Guid, string>> UsernamesAsync(FinanceTrackerContext context,
        List<Domain.Household.HouseholdMember> members, CancellationToken cancellationToken)
    {
        List<Guid> userIds = members.Select(static x => x.LinkedUserId).OfType<Guid>().ToList();
        return await context.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(static x => x.Id, static x => x.UserName ?? string.Empty, cancellationToken);
    }

    private static HouseholdMember ToContract(Domain.Household.HouseholdMember householdMember,
        Dictionary<Guid, string> usernames) => new()
    {
        Id = householdMember.Id,
        FirstName = householdMember.FirstName,
        LastName = householdMember.LastName,
        YearlyIncome = householdMember.YearlyIncome,
        PayeeNames = householdMember.PayeeNames.ToList(),
        LinkedAccounts = householdMember.LinkedAccounts
            .Select(static x => new LinkedAccount
            {
                SortCode = x.SortCode ?? string.Empty,
                Number = x.Number ?? string.Empty
            })
            .ToList(),
        LinkedUserId = householdMember.LinkedUserId,
        LinkedUsername = householdMember.LinkedUserId is { } linkedUserId
            ? usernames.GetValueOrDefault(linkedUserId)
            : null
    };
}
