using FinanceTracker.Domain.Sharing;

namespace FinanceTracker.Tests.Unit.Domain.Sharing;

[Category("Domain")]
public class UserLinkTests
{
    private static readonly Guid Inviter = Guid.NewGuid();
    private static readonly Guid Invitee = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();
    private static readonly DateTime InvitedAt = new(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = InvitedAt.AddDays(1);

    [Test]
    public async Task New_IsWaitingForTheInviteeAndOwnedByTheInviter()
    {
        UserLink link = new(Inviter, Invitee, InvitedAt);

        using (Assert.Multiple())
        {
            await Assert.That(link.UserId).IsEqualTo(Inviter);
            await Assert.That(link.InviteeId).IsEqualTo(Invitee);
            await Assert.That(link.Status).IsEqualTo(UserLinkStatus.Pending);
            await Assert.That(link.IsLive).IsTrue();
            await Assert.That(link.InvitedAt).IsEqualTo(InvitedAt);
        }
    }

    [Test]
    public void New_WithThemselves_Throws() =>
        Assert.Throws<ArgumentException>(() => _ = new UserLink(Inviter, Inviter, InvitedAt));

    [Test]
    public async Task OtherThan_IsTheOtherPersonFromEitherSide()
    {
        UserLink link = new(Inviter, Invitee, InvitedAt);

        using (Assert.Multiple())
        {
            await Assert.That(link.OtherThan(Inviter)).IsEqualTo(Invitee);
            await Assert.That(link.OtherThan(Invitee)).IsEqualTo(Inviter);
            await Assert.That(link.Involves(Stranger)).IsFalse();
        }
    }

    [Test]
    public void OtherThan_Stranger_Throws() =>
        Assert.Throws<ArgumentException>(() => new UserLink(Inviter, Invitee, InvitedAt).OtherThan(Stranger));

    [Test]
    public async Task Accept_ByTheInvitee_LinksThem()
    {
        UserLink link = new(Inviter, Invitee, InvitedAt);

        link.Accept(Invitee, Later);

        using (Assert.Multiple())
        {
            await Assert.That(link.Status).IsEqualTo(UserLinkStatus.Accepted);
            await Assert.That(link.AnsweredAt).IsEqualTo(Later);
            await Assert.That(link.IsLive).IsTrue();
        }
    }

    [Test]
    public async Task Decline_ByTheInvitee_IsNoLongerLive()
    {
        UserLink link = new(Inviter, Invitee, InvitedAt);

        link.Decline(Invitee, Later);

        using (Assert.Multiple())
        {
            await Assert.That(link.Status).IsEqualTo(UserLinkStatus.Declined);
            await Assert.That(link.AnsweredAt).IsEqualTo(Later);
            await Assert.That(link.IsLive).IsFalse();
        }
    }

    [Test]
    public void Accept_ByTheInviter_Throws() =>
        Assert.Throws<InvalidOperationException>(() => new UserLink(Inviter, Invitee, InvitedAt).Accept(Inviter, Later));

    [Test]
    public void Accept_ByAStranger_Throws() =>
        Assert.Throws<InvalidOperationException>(() => new UserLink(Inviter, Invitee, InvitedAt).Accept(Stranger, Later));

    [Test]
    public void Accept_Twice_Throws()
    {
        UserLink link = new(Inviter, Invitee, InvitedAt);
        link.Accept(Invitee, Later);

        Assert.Throws<InvalidOperationException>(() => link.Accept(Invitee, Later));
    }

    [Test]
    public void Decline_AfterAccepting_Throws()
    {
        UserLink link = new(Inviter, Invitee, InvitedAt);
        link.Accept(Invitee, Later);

        Assert.Throws<InvalidOperationException>(() => link.Decline(Invitee, Later));
    }

    [Test]
    public async Task End_ByTheInviterWhileWaiting_WithdrawsIt()
    {
        UserLink link = new(Inviter, Invitee, InvitedAt);

        link.End(Inviter, Later);

        using (Assert.Multiple())
        {
            await Assert.That(link.Status).IsEqualTo(UserLinkStatus.Ended);
            await Assert.That(link.EndedAt).IsEqualTo(Later);
            await Assert.That(link.EndedBy).IsEqualTo(Inviter);
            await Assert.That(link.IsLive).IsFalse();
        }
    }

    [Test]
    public void End_ByTheInviteeWhileWaiting_Throws_TheyDeclineInstead() =>
        Assert.Throws<InvalidOperationException>(() => new UserLink(Inviter, Invitee, InvitedAt).End(Invitee, Later));

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task End_OnceLinked_EitherOfThemCan(bool byInviter)
    {
        UserLink link = new(Inviter, Invitee, InvitedAt);
        link.Accept(Invitee, Later);
        Guid endedBy = byInviter ? Inviter : Invitee;

        link.End(endedBy, Later.AddDays(1));

        using (Assert.Multiple())
        {
            await Assert.That(link.Status).IsEqualTo(UserLinkStatus.Ended);
            await Assert.That(link.EndedBy).IsEqualTo(endedBy);
        }
    }

    [Test]
    public void End_ByAStranger_Throws()
    {
        UserLink link = new(Inviter, Invitee, InvitedAt);
        link.Accept(Invitee, Later);

        Assert.Throws<InvalidOperationException>(() => link.End(Stranger, Later));
    }

    [Test]
    public void End_Twice_Throws()
    {
        UserLink link = new(Inviter, Invitee, InvitedAt);
        link.End(Inviter, Later);

        Assert.Throws<InvalidOperationException>(() => link.End(Inviter, Later));
    }

    [Test]
    public async Task PairOf_IsTheSameWhoeverIsFirst_AndSetOnTheLinkEitherWay()
    {
        UserLink forward = new(Inviter, Invitee, InvitedAt);
        UserLink backward = new(Invitee, Inviter, InvitedAt);

        using (Assert.Multiple())
        {
            await Assert.That(UserLink.PairOf(Inviter, Invitee)).IsEqualTo(UserLink.PairOf(Invitee, Inviter));
            await Assert.That((forward.FirstOfPair, forward.SecondOfPair))
                .IsEqualTo((backward.FirstOfPair, backward.SecondOfPair));
            await Assert.That(forward.InvitedBy).IsEqualTo(Inviter);
        }
    }

    [Test]
    public async Task InviteAgain_AfterADecline_ByTheOtherOfThem_TheyAnswerItNow()
    {
        // Arrange
        UserLink link = new(Inviter, Invitee, InvitedAt);
        link.Decline(Invitee, Later);

        // Act
        link.InviteAgain(Invitee, Later.AddDays(1));

        // Assert, the first inviter answers this time, and the first invitee can't
        using (Assert.Multiple())
        {
            await Assert.That((link.Status, link.InvitedBy, link.InvitedUserId, link.InvitedAt, link.AnsweredAt))
                .IsEqualTo((UserLinkStatus.Pending, Invitee, Inviter, Later.AddDays(1), (DateTime?)null));
            await Assert.That(() => link.Accept(Invitee, Later)).Throws<InvalidOperationException>();
            await Assert.That(() => link.End(Inviter, Later)).Throws<InvalidOperationException>();
        }

        link.Accept(Inviter, Later.AddDays(2));
        await Assert.That(link.Status).IsEqualTo(UserLinkStatus.Accepted);
    }

    [Test]
    public async Task InviteAgain_AfterItEnded_ClearsTheEnd()
    {
        UserLink link = new(Inviter, Invitee, InvitedAt);
        link.Accept(Invitee, Later);
        link.End(Invitee, Later.AddDays(1));

        link.InviteAgain(Inviter, Later.AddDays(2));

        await Assert.That((link.Status, link.EndedAt, link.EndedBy))
            .IsEqualTo((UserLinkStatus.Pending, (DateTime?)null, (Guid?)null));
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public void InviteAgain_WhileWaitingOrLinked_Throws(bool linked)
    {
        UserLink link = new(Inviter, Invitee, InvitedAt);
        if (linked)
        {
            link.Accept(Invitee, Later);
        }

        Assert.Throws<InvalidOperationException>(() => link.InviteAgain(Invitee, Later));
    }

    [Test]
    public void InviteAgain_ByAStranger_Throws()
    {
        UserLink link = new(Inviter, Invitee, InvitedAt);
        link.Decline(Invitee, Later);

        Assert.Throws<InvalidOperationException>(() => link.InviteAgain(Stranger, Later));
    }
}
