using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HouseholdMember = FinanceTracker.Contracts.HouseholdMember.HouseholdMember;

namespace FinanceTracker.Infrastructure.HouseholdMembers;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IHouseholdMemberService>]
public class HouseholdMemberService : ServiceBase<HouseholdMemberService>, IHouseholdMemberService
{
    public HouseholdMemberService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<HouseholdMemberService> logger) : base(currentUser,
        financeTrackerContextFactory, logger)
    {
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
            yield return new HouseholdMember
            {
                Id = householdMember.Id,
                FirstName = householdMember.FirstName,
                LastName = householdMember.LastName,
                Income = householdMember.Income
            };
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

        return new HouseholdMember
        {
            Id = householdMember.Id,
            FirstName = householdMember.FirstName,
            LastName = householdMember.LastName,
            Income = householdMember.Income
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
        return true;

    }
}
