using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Infrastructure.HouseholdMembers;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using HouseholdMember = FinanceTracker.Contracts.HouseholdMember.HouseholdMember;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.HouseholdMembers;

public class HouseholdMemberServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    [Test]
    public async Task AddHouseholdMemberAsync_PersistsForCurrentUserAndReturnsMapped()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        HouseholdMemberService subject = BuildSubject();

        // Act
        HouseholdMember added = await subject.AddHouseholdMemberAsync(new AddHouseholdMember
        {
            FirstName = "Jane",
            LastName = "Doe",
            Income = 1200
        }, CancellationToken);

        // Assert
        await using var context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Domain.Household.HouseholdMember persisted = await context.HouseholdMembers.SingleAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(added.FirstName).IsEqualTo("Jane");
            await Assert.That(added.LastName).IsEqualTo("Doe");
            await Assert.That(added.Income).IsEqualTo(1200m);
            await Assert.That(persisted.UserId).IsEqualTo(Scenario.UserMappings[Username]);
        }
    }

    [Test]
    public async Task AddHouseholdMemberAsync_NegativeIncome_ThrowsAndPersistsNothing()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        HouseholdMemberService subject = BuildSubject();

        // Act
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => subject.AddHouseholdMemberAsync(
            new AddHouseholdMember { FirstName = "Jane", LastName = "Doe", Income = -1 }, CancellationToken));

        // Assert
        await using var context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        await Assert.That(await context.HouseholdMembers.CountAsync(CancellationToken)).IsEqualTo(0);
    }

    [Test]
    public async Task GetHouseholdMembersAsync_ReturnsOnlyCurrentUsersMembers()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.HouseholdMemberScenario.WithHouseholdMember("partner", Username, userId =>
                new Domain.Household.HouseholdMember(userId, "Jane", "Doe", null)))
            .Do(s => s.HouseholdMemberScenario.WithHouseholdMember("theirs", OtherUsername, userId =>
                new Domain.Household.HouseholdMember(userId, "Other", "User", 15)));

        HouseholdMemberService subject = BuildSubject();

        // Act
        List<HouseholdMember> members = await subject.GetHouseholdMembersAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        HouseholdMember member = members.Single();

        using (Assert.Multiple())
        {
            await Assert.That(member.FirstName).IsEqualTo("Jane");
            await Assert.That(member.LastName).IsEqualTo("Doe");
            await Assert.That(member.Income).IsNull();
        }
    }

    [Test]
    public async Task GetHouseholdMembersAsync_WhenUserHasNoMembers_ReturnsEmpty()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        HouseholdMemberService subject = BuildSubject();

        // Act
        List<HouseholdMember> members = await subject.GetHouseholdMembersAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(members.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DeleteHouseholdMemberAsync_WhenExists_RemovesMemberAndReturnsTrue()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.HouseholdMemberScenario.WithHouseholdMember("partner", Username));

        HouseholdMemberService subject = BuildSubject();

        // Act
        bool deleted = await subject.DeleteHouseholdMemberAsync(Scenario.HouseholdMemberMappings["partner"],
            CancellationToken);

        // Assert
        await using var context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(deleted).IsTrue();
            await Assert.That(await context.HouseholdMembers.CountAsync(CancellationToken)).IsEqualTo(0);
        }
    }

    [Test]
    public async Task DeleteHouseholdMemberAsync_WhenNotFound_ReturnsFalse()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        HouseholdMemberService subject = BuildSubject();

        // Act
        bool deleted = await subject.DeleteHouseholdMemberAsync(Guid.NewGuid(), CancellationToken);

        // Assert
        await Assert.That(deleted).IsFalse();
    }

    [Test]
    public async Task DeleteHouseholdMemberAsync_WhenOwnedByAnotherUser_ReturnsFalseAndKeepsMember()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.HouseholdMemberScenario.WithHouseholdMember("theirs", OtherUsername));

        HouseholdMemberService subject = BuildSubject();

        // Act
        bool deleted = await subject.DeleteHouseholdMemberAsync(Scenario.HouseholdMemberMappings["theirs"],
            CancellationToken);

        // Assert
        await using var context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(deleted).IsFalse();
            await Assert.That(await context.HouseholdMembers.CountAsync(CancellationToken)).IsEqualTo(1);
        }
    }

    private HouseholdMemberService BuildSubject() =>
        new(UserPrincipal(Username), DbContextFactory, NullLogger<HouseholdMemberService>.Instance);
}
