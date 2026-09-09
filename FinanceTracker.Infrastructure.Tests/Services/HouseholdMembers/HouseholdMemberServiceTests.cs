using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Domain;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.HouseholdMembers;
using FinanceTracker.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Infrastructure.Tests.Services.HouseholdMembers;

public class HouseholdMemberServiceTests : ServiceTestsFixtureBase
{
    [Test]
    public async Task AddAndGetHouseholdMembersAsync_ReturnsOnlyCurrentUsersMembers()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"household-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        await using (FinanceTrackerContext context = await factory.CreateDbContextAsync(_cancellationTokenSource.Token))
        {
            FinanceTrackerUser secondary = await context.Users.Include(x => x.HouseholdMembers)
                .SingleAsync(x => x.Id == SecondaryUserId, _cancellationTokenSource.Token);
            secondary.HouseholdMembers!.Add(new HouseholdMember
            {
                FirstName = "Other",
                LastName = "User",
                Income = 15,
                Created = DateTime.UtcNow
            });
            await context.SaveChangesAsync(_cancellationTokenSource.Token);
        }

        var subject = new HouseholdMemberService(BuildUser(PrimaryUserId), factory, NullLogger<HouseholdMemberService>.Instance);
        await subject.AddHouseholdMemberAsync(new AddHouseholdMemberRequest
        {
            FirstName = "Jane",
            LastName = "Doe",
            Income = 1200
        }, _cancellationTokenSource.Token);

        List<string> names = [];
        await foreach (var member in subject.GetHouseholdMembersAsync(_cancellationTokenSource.Token))
        {
            names.Add(member.FirstName);
        }

        await Assert.That(names).IsEquivalentTo(["Jane"]);
    }

    [Test]
    public async Task GetHouseholdMembersAsync_WhenUserHasNoMembers_ReturnsEmpty()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"household-empty-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        var subject = new HouseholdMemberService(BuildUser(PrimaryUserId), factory, NullLogger<HouseholdMemberService>.Instance);

        var names = new List<string>();
        await foreach (var member in subject.GetHouseholdMembersAsync(_cancellationTokenSource.Token))
        {
            names.Add(member.FirstName);
        }

        await Assert.That(names.Count).IsEqualTo(0);
    }
}
