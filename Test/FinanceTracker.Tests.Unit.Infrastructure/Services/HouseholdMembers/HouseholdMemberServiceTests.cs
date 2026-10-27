using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.HouseholdMembers;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
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
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
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
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
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
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

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
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(deleted).IsFalse();
            await Assert.That(await context.HouseholdMembers.CountAsync(CancellationToken)).IsEqualTo(1);
        }
    }

    [Test]
    public async Task UpdateRecognitionAsync_SavesAndRecognisesTheirTransactionsStraightAway()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.HouseholdMemberScenario.WithHouseholdMember("partner", Username))
            .Do(s => s.TransactionScenario.WithTransaction("by-name", "joint", Details("J Smith 01JAN26")))
            .Do(s => s.TransactionScenario.WithTransaction("by-account", "joint",
                Details("Bills", reference: "112233 12345678")))
            .Do(s => s.TransactionScenario.WithTransaction("shopping", "joint", Details("Tesco")));

        Guid partnerId = Scenario.HouseholdMemberMappings["partner"];
        HouseholdMemberService subject = BuildSubject();

        // Act
        HouseholdMemberRecognitionSaved saved = await subject.UpdateRecognitionAsync(partnerId,
            new UpdateHouseholdMemberRecognition
            {
                PayeeNames = [" J Smith "],
                LinkedAccounts = [new LinkedAccount { SortCode = "11-22-33", Number = "12345678" }]
            }, CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Domain.Household.HouseholdMember persisted = await context.HouseholdMembers.SingleAsync(CancellationToken);
        Dictionary<Guid, BankTransaction> transactions =
            await context.BankTransactions.ToDictionaryAsync(x => x.Id, CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(saved.RecognisedTransactions).IsEqualTo(2);
            await Assert.That(saved.Member.PayeeNames).IsEquivalentTo(["J Smith"]);
            await Assert.That(saved.Member.LinkedAccounts.Single().SortCode).IsEqualTo("112233");
            await Assert.That(persisted.PayeeNames).IsEquivalentTo(["J Smith"]);
            await Assert.That(transactions[Scenario.TransactionMappings["by-name"]].Transfer!.PartnerHouseholdMemberId)
                .IsEqualTo(partnerId);
            await Assert.That(transactions[Scenario.TransactionMappings["shopping"]].Transfer).IsNull();
        }
    }

    [Test]
    public async Task UpdateRecognitionAsync_WhenOwnedByAnotherUser_ThrowsAndChangesNothing()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.HouseholdMemberScenario.WithHouseholdMember("theirs", OtherUsername));

        HouseholdMemberService subject = BuildSubject();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => subject.UpdateRecognitionAsync(
            Scenario.HouseholdMemberMappings["theirs"],
            new UpdateHouseholdMemberRecognition { PayeeNames = ["Someone"] }, CancellationToken));

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        await Assert.That((await context.HouseholdMembers.SingleAsync(CancellationToken)).PayeeNames).IsEmpty();
    }

    [Test]
    public async Task DeleteHouseholdMemberAsync_TheirTransactionsCountAgain()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.HouseholdMemberScenario.WithHouseholdMember("partner", Username))
            .Do(s => s.HouseholdMemberScenario.WithPayeeName("partner", "J Smith"))
            .Do(s => s.TransactionScenario.WithTransaction("by-name", "joint", Details("J Smith")));

        HouseholdMemberService subject = BuildSubject();
        await BuildMatcher().MatchAsync(CancellationToken);

        // Act
        await subject.DeleteHouseholdMemberAsync(Scenario.HouseholdMemberMappings["partner"], CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        await Assert.That((await context.BankTransactions.SingleAsync(CancellationToken)).Transfer).IsNull();
    }

    private Task<TestingScenario> SeedAccountAsync() =>
        Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("joint", "bank"));

    private static TransactionDetails Details(string description, string? reference = null) =>
        new TransactionDetailsFaker().Generate() with
        {
            Description = description,
            IsPending = false,
            LabelledAsTransfer = false,
            Metadata = new TransactionMetadata(reference, null, null, null, null)
        };

    private TransferMatchingService BuildMatcher() =>
        new(CurrentUserFor(Username), DbContextFactory, NullLogger<TransferMatchingService>.Instance,
            TimeProvider.System);

    private HouseholdMemberService BuildSubject() =>
        new(CurrentUserFor(Username), DbContextFactory, NullLogger<HouseholdMemberService>.Instance, BuildMatcher());
}
