using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transfers;
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
        await foreach (Domain.Household.HouseholdMember householdMember in context.HouseholdMembers.OwnedBy(UserId)
                           .AsNoTracking()
                           .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return ToContract(householdMember);
        }
    }

    public async Task<HouseholdMember> AddHouseholdMemberAsync(AddHouseholdMember categoryToAdd,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        Domain.Household.HouseholdMember householdMember = new(UserId, categoryToAdd.FirstName,
            categoryToAdd.LastName, categoryToAdd.Income);

        context.HouseholdMembers.Add(householdMember);
        await context.SaveChangesAsync(cancellationToken);

        return ToContract(householdMember);
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

        // Applied to the transactions already there now, rather than waiting for the next sync
        await _transferMatchingService.MatchAsync(cancellationToken);

        int recognised = await context.BankTransactions.OwnedBy(UserId)
            .CountAsync(x => x.Transfer != null && x.Transfer.Decision == TransferDecision.Transfer &&
                             x.Transfer.PartnerHouseholdMemberId == id, cancellationToken);

        return new HouseholdMemberRecognitionSaved
        {
            Member = ToContract(householdMember),
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

        // Their transactions were transfers because of them, so count again or go back to review
        await _transferMatchingService.MatchAsync(cancellationToken);
        return true;

    }

    private static HouseholdMember ToContract(Domain.Household.HouseholdMember householdMember) => new()
    {
        Id = householdMember.Id,
        FirstName = householdMember.FirstName,
        LastName = householdMember.LastName,
        Income = householdMember.Income,
        PayeeNames = householdMember.PayeeNames.ToList(),
        LinkedAccounts = householdMember.LinkedAccounts
            .Select(static x => new LinkedAccount
            {
                SortCode = x.SortCode ?? string.Empty,
                Number = x.Number ?? string.Empty
            })
            .ToList()
    };
}
