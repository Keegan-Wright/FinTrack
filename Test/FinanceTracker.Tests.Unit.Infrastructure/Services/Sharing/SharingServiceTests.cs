using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using DomainLink = FinanceTracker.Domain.Sharing.UserLink;
using UserLinkStatus = FinanceTracker.Domain.Sharing.UserLinkStatus;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Sharing;

public class SharingServiceTests : InfrastructureTestBase
{
    private const string Alex = "alex";
    private const string Sam = "sam";
    private const string Jo = "jo";

    private readonly List<(Guid UserId, NotificationKind Kind, string Title)> _raised = [];
    private ServiceProvider _services = null!;

    [Before(Test)]
    public void SetUpNotifier()
    {
        ServiceCollection services = new();
        services.AddCurrentUser();
        services.AddScoped<INotifier>(sp => new RecordingNotifier(sp.GetRequiredService<ICurrentUser>(), _raised));
        services.AddTransferMatching(DbContextFactory);
        _services = services.BuildServiceProvider();
    }

    [After(Test)]
    public async Task TearDownNotifier() => await _services.DisposeAsync();

    [Test]
    public async Task InviteAsync_ByUsernameInAnyCase_WaitsForThemAndTellsThem()
    {
        // Arrange
        await SeedUsersAsync();

        // Act
        InviteOutcome outcome = await Subject(Alex).InviteAsync(new InviteToShare { Who = "SAM" }, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(outcome.Refusal).IsNull();
            await Assert.That(outcome.Link!.State).IsEqualTo(LinkState.Invited);
            await Assert.That(outcome.Link.Username).IsEqualTo(Sam);
            await Assert.That(_raised.Select(static x => (x.UserId, x.Kind)))
                .IsEquivalentTo([(Scenario.UserMappings[Sam], NotificationKind.SharingInvite)]);
        }
    }

    [Test]
    public async Task InviteAsync_ByEmail_FindsThem()
    {
        // Arrange
        await SeedUsersAsync();
        await Scenario.Do(s => s.UserScenario.WithEmail(Sam, "sam@example.test"));

        // Act
        InviteOutcome outcome = await Subject(Alex)
            .InviteAsync(new InviteToShare { Who = " Sam@Example.TEST " }, CancellationToken);

        // Assert
        await Assert.That(outcome.Link!.Username).IsEqualTo(Sam);
    }

    [Test]
    public async Task InviteAsync_AUsernameThatsAlsoSomeoneElsesEmail_FindsTheUsername()
    {
        // Arrange, Jo's email is Sam's username
        await SeedUsersAsync();
        await Scenario.Do(s => s.UserScenario.WithEmail(Jo, Sam));

        // Act
        InviteOutcome outcome = await Subject(Alex).InviteAsync(new InviteToShare { Who = Sam }, CancellationToken);

        // Assert
        await Assert.That(outcome.Link!.Username).IsEqualTo(Sam);
    }

    [Test]
    public async Task InviteAsync_BothInvitingEachOtherAtOnce_OnlyOneLinkIsMade()
    {
        // Arrange
        await SeedUsersAsync();

        // Act
        InviteOutcome[] outcomes = await Task.WhenAll(
            Subject(Alex).InviteAsync(new InviteToShare { Who = Sam }, CancellationToken),
            Subject(Sam).InviteAsync(new InviteToShare { Who = Alex }, CancellationToken));

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(outcomes.Count(static x => x.Link is not null)).IsEqualTo(1);
            await Assert.That(outcomes.Count(static x => x.Refusal == InviteRefusal.AlreadyInvited)).IsEqualTo(1);
            await Assert.That(await context.UserLinks.CountAsync(CancellationToken)).IsEqualTo(1);
        }
    }

    [Test]
    public async Task TwoLinksBetweenThePairEitherWay_CantBeSaved()
    {
        // Arrange
        await SeedUsersAsync();
        Guid alex = Scenario.UserMappings[Alex];
        Guid sam = Scenario.UserMappings[Sam];
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        context.UserLinks.Add(new DomainLink(alex, sam, DateTime.UtcNow));
        await context.SaveChangesAsync(CancellationToken);

        // Act
        context.UserLinks.Add(new DomainLink(sam, alex, DateTime.UtcNow));

        // Assert
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(CancellationToken));
    }

    [Test]
    public async Task InviteAsync_AfterTheyDeclined_TheOtherInvites_OpensTheSameLinkAgain()
    {
        // Arrange
        await SeedUsersAsync();
        InviteOutcome first = await Subject(Alex).InviteAsync(new InviteToShare { Who = Sam }, CancellationToken);
        await Subject(Sam).DeclineAsync(first.Link!.Id, CancellationToken);
        _raised.Clear();

        // Act
        InviteOutcome again = await Subject(Sam).InviteAsync(new InviteToShare { Who = Alex }, CancellationToken);
        List<UserLink> alexs = await Subject(Alex).GetLinksAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert, Alex answers this time, and is told of it again
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(again.Link!.Id).IsEqualTo(first.Link.Id);
            await Assert.That(alexs.Select(static x => x.State)).IsEquivalentTo([LinkState.InvitedYou]);
            await Assert.That(await context.UserLinks.CountAsync(CancellationToken)).IsEqualTo(1);
            await Assert.That(_raised.Select(static x => (x.UserId, x.Kind)))
                .IsEquivalentTo([(Scenario.UserMappings[Alex], NotificationKind.SharingInvite)]);
            await Assert.That(await Subject(Alex).AcceptAsync(first.Link.Id, CancellationToken)).IsTrue();
        }
    }

    [Test]
    public async Task GetPeopleAsync_OnlyThoseLinked_AndSharesSayWhoAndSinceWhen()
    {
        // Arrange, linked with Sam, and an invite waiting for Jo
        await SeedUsersAsync();
        Guid linkId = await LinkAsync(Alex, Sam);
        await Subject(Alex).InviteAsync(new InviteToShare { Who = Jo }, CancellationToken);

        // Act
        List<UserLink> people = await Subject(Alex).GetPeopleAsync(CancellationToken).ToListAsync(CancellationToken);
        LinkShares shares = (await Subject(Alex).GetSharesAsync(linkId, CancellationToken))!;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(people.Select(static x => (x.Username, x.State)))
                .IsEquivalentTo([(Sam, LinkState.Linked)]);
            await Assert.That(shares.Username).IsEqualTo(Sam);
            await Assert.That(shares.Since).IsEqualTo(people.Single().Since);
        }
    }

    [Test]
    public async Task InviteAsync_NoOneByThatName_IsRefused()
    {
        // Arrange
        await SeedUsersAsync();

        // Act
        InviteOutcome outcome = await Subject(Alex).InviteAsync(new InviteToShare { Who = "nobody" }, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(outcome.Refusal).IsEqualTo(InviteRefusal.NoSuchUser);
            await Assert.That(await CountLinksAsync()).IsEqualTo(0);
            await Assert.That(_raised).IsEmpty();
        }
    }

    [Test]
    public async Task InviteAsync_Themselves_IsRefused()
    {
        // Arrange
        await SeedUsersAsync();

        // Act
        InviteOutcome outcome = await Subject(Alex).InviteAsync(new InviteToShare { Who = Alex }, CancellationToken);

        // Assert
        await Assert.That(outcome.Refusal).IsEqualTo(InviteRefusal.Yourself);
    }

    [Test]
    [Arguments(Alex, Sam)]
    [Arguments(Sam, Alex)]
    public async Task InviteAsync_InviteAlreadyWaitingEitherWay_IsRefused(string firstInviter, string firstInvitee)
    {
        // Arrange
        await SeedUsersAsync();
        await Subject(firstInviter).InviteAsync(new InviteToShare { Who = firstInvitee }, CancellationToken);

        // Act
        InviteOutcome outcome = await Subject(Alex).InviteAsync(new InviteToShare { Who = Sam }, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(outcome.Refusal).IsEqualTo(InviteRefusal.AlreadyInvited);
            await Assert.That(await CountLinksAsync()).IsEqualTo(1);
        }
    }

    [Test]
    public async Task InviteAsync_AlreadyLinked_IsRefused()
    {
        // Arrange
        await SeedUsersAsync();
        Guid id = await LinkAsync(Alex, Sam);

        // Act
        InviteOutcome outcome = await Subject(Sam).InviteAsync(new InviteToShare { Who = Alex }, CancellationToken);

        // Assert
        await Assert.That(outcome.Refusal).IsEqualTo(InviteRefusal.AlreadyLinked);
        await Assert.That(id).IsNotEqualTo(Guid.Empty);
    }

    [Test]
    public async Task GetLinksAsync_ShowsEachSideItsOwnViewAndNoOneElse()
    {
        // Arrange
        await SeedUsersAsync();
        await Subject(Alex).InviteAsync(new InviteToShare { Who = Sam }, CancellationToken);

        // Act
        List<UserLink> alexSees = await LinksOfAsync(Alex);
        List<UserLink> samSees = await LinksOfAsync(Sam);
        List<UserLink> joSees = await LinksOfAsync(Jo);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(alexSees.Select(static x => (x.Username, x.State))).IsEquivalentTo([(Sam, LinkState.Invited)]);
            await Assert.That(samSees.Select(static x => (x.Username, x.State))).IsEquivalentTo([(Alex, LinkState.InvitedYou)]);
            await Assert.That(joSees).IsEmpty();
        }
    }

    [Test]
    public async Task AcceptAsync_ByTheInvitee_LinksBothAndTellsTheInviter()
    {
        // Arrange
        await SeedUsersAsync();
        InviteOutcome sent = await Subject(Alex).InviteAsync(new InviteToShare { Who = Sam }, CancellationToken);
        _raised.Clear();

        // Act
        bool accepted = await Subject(Sam).AcceptAsync(sent.Link!.Id, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(accepted).IsTrue();
            await Assert.That((await LinksOfAsync(Alex)).Single().State).IsEqualTo(LinkState.Linked);
            await Assert.That((await LinksOfAsync(Sam)).Single().State).IsEqualTo(LinkState.Linked);
            await Assert.That(_raised.Select(static x => (x.UserId, x.Kind)))
                .IsEquivalentTo([(Scenario.UserMappings[Alex], NotificationKind.SharingAccepted)]);
        }
    }

    [Test]
    [Arguments(Alex)]
    [Arguments(Jo)]
    public async Task AcceptAsync_BySomeoneOtherThanTheInvitee_IsNotFound(string answering)
    {
        // Arrange
        await SeedUsersAsync();
        InviteOutcome sent = await Subject(Alex).InviteAsync(new InviteToShare { Who = Sam }, CancellationToken);

        // Act
        bool accepted = await Subject(answering).AcceptAsync(sent.Link!.Id, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(accepted).IsFalse();
            await Assert.That(await StatusOfAsync(sent.Link.Id)).IsEqualTo(UserLinkStatus.Pending);
        }
    }

    [Test]
    public async Task DeclineAsync_ByTheInvitee_GoesFromBothListsAndCanBeSentAgain()
    {
        // Arrange
        await SeedUsersAsync();
        InviteOutcome sent = await Subject(Alex).InviteAsync(new InviteToShare { Who = Sam }, CancellationToken);

        // Act
        bool declined = await Subject(Sam).DeclineAsync(sent.Link!.Id, CancellationToken);
        UserLinkStatus afterDeclining = await StatusOfAsync(sent.Link.Id);
        InviteOutcome again = await Subject(Alex).InviteAsync(new InviteToShare { Who = Sam }, CancellationToken);

        // Assert, sent again as the same link, their one link
        using (Assert.Multiple())
        {
            await Assert.That(declined).IsTrue();
            await Assert.That(afterDeclining).IsEqualTo(UserLinkStatus.Declined);
            await Assert.That(again.Refusal).IsNull();
            await Assert.That(again.Link!.Id).IsEqualTo(sent.Link.Id);
            await Assert.That(await StatusOfAsync(sent.Link.Id)).IsEqualTo(UserLinkStatus.Pending);
        }
    }

    [Test]
    public async Task EndAsync_TheInviterWithdrawsAWaitingInvite()
    {
        // Arrange
        await SeedUsersAsync();
        InviteOutcome sent = await Subject(Alex).InviteAsync(new InviteToShare { Who = Sam }, CancellationToken);

        // Act
        bool ended = await Subject(Alex).EndAsync(sent.Link!.Id, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ended).IsTrue();
            await Assert.That(await LinksOfAsync(Sam)).IsEmpty();
        }
    }

    [Test]
    public async Task EndAsync_TheInviteeCantEndAWaitingInvite_TheyDecline()
    {
        // Arrange
        await SeedUsersAsync();
        InviteOutcome sent = await Subject(Alex).InviteAsync(new InviteToShare { Who = Sam }, CancellationToken);

        // Act
        bool ended = await Subject(Sam).EndAsync(sent.Link!.Id, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ended).IsFalse();
            await Assert.That(await StatusOfAsync(sent.Link.Id)).IsEqualTo(UserLinkStatus.Pending);
        }
    }

    [Test]
    [Arguments(Alex)]
    [Arguments(Sam)]
    public async Task EndAsync_OnceLinked_EitherCanEndIt(string ending)
    {
        // Arrange
        await SeedUsersAsync();
        Guid id = await LinkAsync(Alex, Sam);

        // Act
        bool ended = await Subject(ending).EndAsync(id, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ended).IsTrue();
            await Assert.That(await LinksOfAsync(Alex)).IsEmpty();
            await Assert.That(await LinksOfAsync(Sam)).IsEmpty();
        }
    }

    [Test]
    public async Task EndAsync_SomeoneElsesLink_IsNotFound()
    {
        // Arrange
        await SeedUsersAsync();
        Guid id = await LinkAsync(Alex, Sam);

        // Act
        bool ended = await Subject(Jo).EndAsync(id, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ended).IsFalse();
            await Assert.That(await StatusOfAsync(id)).IsEqualTo(UserLinkStatus.Accepted);
        }
    }

    private Task<TestingScenario> SeedUsersAsync() =>
        Scenario.Do(s => s.UserScenario.WithUser(Alex))
            .Do(s => s.UserScenario.WithUser(Sam))
            .Do(s => s.UserScenario.WithUser(Jo));

    /// <summary>
    /// <paramref name="inviter"/> invites <paramref name="invitee"/>, who accepts.
    /// </summary>
    private async Task<Guid> LinkAsync(string inviter, string invitee)
    {
        InviteOutcome sent = await Subject(inviter).InviteAsync(new InviteToShare { Who = invitee }, CancellationToken);
        await Subject(invitee).AcceptAsync(sent.Link!.Id, CancellationToken);
        return sent.Link.Id;
    }

    private Task<List<UserLink>> LinksOfAsync(string username) =>
        Subject(username).GetLinksAsync(CancellationToken).ToListAsync(CancellationToken).AsTask();

    private async Task<int> CountLinksAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.UserLinks.CountAsync(CancellationToken);
    }

    private async Task<UserLinkStatus> StatusOfAsync(Guid id)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        DomainLink link = await context.UserLinks.SingleAsync(x => x.Id == id, CancellationToken);
        return link.Status;
    }

    private SharingService Subject(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<SharingService>.Instance,
            _services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
}
