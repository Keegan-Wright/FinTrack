using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Tests.Integration.Http;

namespace FinanceTracker.Tests.Integration.Api;

public class SharingApiTests : IntegrationTestBase
{
    [Test]
    public async Task InviteAcceptAndEnd_ThroughTheApi_EachSideSeesItsOwnView()
    {
        // Arrange
        SignedInUser alex = await RegisterUserAsync();
        SignedInUser sam = await RegisterUserAsync();
        ISharingService alexApi = ApiFor(alex).Get<ISharingService>();
        ISharingService samApi = ApiFor(sam).Get<ISharingService>();

        // Act
        InviteOutcome sent = await alexApi.InviteAsync(new InviteToShare { Who = sam.Username.ToUpperInvariant() },
            CancellationToken);
        List<UserLink> samWaiting = await samApi.GetLinksAsync(CancellationToken).ToListAsync(CancellationToken);
        bool accepted = await samApi.AcceptAsync(sent.Link!.Id, CancellationToken);
        List<UserLink> alexLinked = await alexApi.GetLinksAsync(CancellationToken).ToListAsync(CancellationToken);
        bool ended = await samApi.EndAsync(sent.Link.Id, CancellationToken);
        List<UserLink> alexAfter = await alexApi.GetLinksAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(sent.Refusal).IsNull();
            await Assert.That(samWaiting.Select(static x => x.State)).IsEquivalentTo([LinkState.InvitedYou]);
            await Assert.That(accepted).IsTrue();
            await Assert.That(alexLinked.Select(static x => (x.Username, x.State)))
                .IsEquivalentTo([(sam.Username, LinkState.Linked)]);
            await Assert.That(ended).IsTrue();
            await Assert.That(alexAfter).IsEmpty();
        }
    }

    [Test]
    public async Task SomeoneElsesLink_CantBeSeenAnsweredOrEnded()
    {
        // Arrange
        SignedInUser alex = await RegisterUserAsync();
        SignedInUser sam = await RegisterUserAsync();
        SignedInUser stranger = await RegisterUserAsync();
        InviteOutcome sent = await ApiFor(alex).Get<ISharingService>()
            .InviteAsync(new InviteToShare { Who = sam.Username }, CancellationToken);
        ISharingService strangerApi = ApiFor(stranger).Get<ISharingService>();

        // Act
        List<UserLink> strangerSees = await strangerApi.GetLinksAsync(CancellationToken).ToListAsync(CancellationToken);
        bool accepted = await strangerApi.AcceptAsync(sent.Link!.Id, CancellationToken);
        bool declined = await strangerApi.DeclineAsync(sent.Link.Id, CancellationToken);
        bool ended = await strangerApi.EndAsync(sent.Link.Id, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(strangerSees).IsEmpty();
            await Assert.That(accepted).IsFalse();
            await Assert.That(declined).IsFalse();
            await Assert.That(ended).IsFalse();
        }
    }

    [Test]
    public async Task Invite_Blank_IsRejectedByValidation()
    {
        // Arrange
        SignedInUser alex = await RegisterUserAsync();

        // Act
        Task Invite() => ApiFor(alex).Get<ISharingService>().InviteAsync(new InviteToShare { Who = " " },
            CancellationToken);

        // Assert
        await Assert.That(Invite).Throws<HttpRequestException>();
    }
}
