using System.Runtime.CompilerServices;
using System.Security.Claims;
using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Domain;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HouseholdMember = FinanceTracker.Contracts.HouseholdMember.HouseholdMember;

namespace FinanceTracker.Infrastructure.HouseholdMembers;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<IHouseholdMemberService>]
public class HouseholdMemberService : ServiceBase<HouseholdMemberService>, IHouseholdMemberService
{
    public HouseholdMemberService(ClaimsPrincipal user,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<HouseholdMemberService> logger) : base(user,
        financeTrackerContextFactory, logger)
    {
    }

    public async IAsyncEnumerable<HouseholdMember> GetHouseholdMembersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (Domain.HouseholdMember householdMember in context.IsolateToUser(UserId)
                           .Include(x => x.HouseholdMembers)
                           .SelectMany(x => x.HouseholdMembers!)
                           .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return new HouseholdMember
            {
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
        FinanceTrackerUser user = await context.IsolateToUser(UserId)
            .Include(x => x.HouseholdMembers)
            .SingleAsync(cancellationToken);

        Domain.HouseholdMember householdMember = new()
        {
            FirstName = categoryToAdd.FirstName,
            LastName = categoryToAdd.LastName,
            Income = categoryToAdd.Income,
            Created = DateTime.Now.ToUniversalTime()
        };

        user.HouseholdMembers!.Add(householdMember);
        await context.SaveChangesAsync(cancellationToken);

        return new HouseholdMember
        {
            FirstName = householdMember.FirstName,
            LastName = householdMember.LastName,
            Income = householdMember.Income
        };
    }

    public async Task<bool> DeleteHouseholdMemberAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        FinanceTrackerUser user = await context.IsolateToUser(UserId)
            .Include(x => x.HouseholdMembers).SingleAsync(cancellationToken);

        Domain.HouseholdMember? householdMember = user.HouseholdMembers!.FirstOrDefault(x => x.Id == id);

        if (householdMember == null)
        {
            return false;
        }

        context.HouseholdMembers.Remove(householdMember);
        await context.SaveChangesAsync(cancellationToken);
        return true;

    }
}
