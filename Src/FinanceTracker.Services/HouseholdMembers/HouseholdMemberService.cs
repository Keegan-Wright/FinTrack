using System.Runtime.CompilerServices;
using System.Security.Claims;
using FinanceTracker.Data;
using FinanceTracker.Data.Models;
using FinanceTracker.Models.Request.HouseholdMember;
using FinanceTracker.Models.Response.HouseholdMember;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Services.HouseholdMembers;

public class HouseholdMemberService : ServiceBase, IHouseholdMemberService
{
    public HouseholdMemberService(ClaimsPrincipal user, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory) : base(user, financeTrackerContextFactory)
    {
    }

    public async IAsyncEnumerable<HouseholdMemberResponse> GetHouseholdMembersAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var context = await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach(var householdMember in context.IsolateToUser(UserId)
                          .Include(x => x.HouseholdMembers)
                          .SelectMany(x => x.HouseholdMembers)
                          .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return new HouseholdMemberResponse()
            {
                FirstName = householdMember.FirstName,
                LastName = householdMember.LastName,
                Income = householdMember.Income
            };
        }
    }

    public async Task<HouseholdMemberResponse> AddHouseholdMemberAsync(AddHouseholdMemberRequest categoryToAdd, CancellationToken cancellationToken)
    {
        await using var context = await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await context.IsolateToUser(UserId)
            .Include(x => x.HouseholdMembers)
            .SingleAsync(cancellationToken: cancellationToken);
            
        var householdMember = new HouseholdMember()
        {
            FirstName = categoryToAdd.FirstName,
            LastName = categoryToAdd.LastName,
            Income = categoryToAdd.Income,
            Created = DateTime.Now.ToUniversalTime()
        };

        user.HouseholdMembers.Add(householdMember);
        await context.SaveChangesAsync(cancellationToken);

        return new HouseholdMemberResponse()
        {
            FirstName = householdMember.FirstName,
            LastName = householdMember.LastName,
            Income = householdMember.Income,
        };
    }

    public async Task<bool> DeleteHouseholdMemberAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await context.IsolateToUser(UserId)
            .Include(x => x.HouseholdMembers).SingleAsync(cancellationToken);
            
        var householdMember = user.HouseholdMembers.FirstOrDefault(x => x.Id == id);

        if (householdMember != null)
        {
            context.HouseholdMembers.Remove(householdMember);
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }

        return false;
    }
}