using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Sharing;

/// <summary>
/// Two users on the instance who've agreed to share with each other. One invites, the other accepts, and either can
/// end it. There's one link between two people, whichever of them invites. Inviting again after a decline or an end
/// opens the same link again, see <see cref="InviteAgain"/>. Owned by whoever first invited,
/// <see cref="AggregateRoot.UserId"/>, though it belongs to both, so it's looked up by <see cref="Involves"/> or
/// <see cref="PairOf"/> rather than by owner.
/// </summary>
public sealed class UserLink : AggregateRoot
{
    [Obsolete("EF Use Only")]
    private UserLink()
    {
    }

    /// <summary>
    /// <paramref name="inviterId"/> inviting <paramref name="inviteeId"/>, waiting for them to answer.
    /// </summary>
    /// <param name="inviterId">The person sending the invite.</param>
    /// <param name="inviteeId">The person invited.</param>
    /// <param name="invitedAt">When it was sent.</param>
    public UserLink(Guid inviterId, Guid inviteeId, DateTime invitedAt) : base(inviterId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(inviteeId, Guid.Empty);
        if (inviteeId == inviterId)
        {
            throw new ArgumentException("A user can't link with themselves.", nameof(inviteeId));
        }

        InviteeId = inviteeId;
        (FirstOfPair, SecondOfPair) = PairOf(inviterId, inviteeId);
        InvitedBy = inviterId;
        InvitedAt = invitedAt;
        Status = UserLinkStatus.Pending;
    }

    /// <summary>
    /// The user first invited, the other of the two beside <see cref="AggregateRoot.UserId"/>. Whoever invites now is
    /// <see cref="InvitedBy"/>.
    /// </summary>
    public Guid InviteeId { get; private set; }

    /// <summary>
    /// The first of the two people, in the order <see cref="PairOf"/> puts them, so the pair is the same whoever
    /// invited whom.
    /// </summary>
    public Guid FirstOfPair { get; private set; }

    /// <summary>
    /// The second of the two people, see <see cref="FirstOfPair"/>.
    /// </summary>
    public Guid SecondOfPair { get; private set; }

    /// <summary>
    /// Who sent the invite last, either of them.
    /// </summary>
    public Guid InvitedBy { get; private set; }

    /// <summary>
    /// Who the invite last went to, the one who answers it.
    /// </summary>
    public Guid InvitedUserId => OtherThan(InvitedBy);

    /// <summary>
    /// Where the link stands.
    /// </summary>
    public UserLinkStatus Status { get; private set; }

    /// <summary>
    /// When the invite was last sent.
    /// </summary>
    public DateTime InvitedAt { get; private set; }

    /// <summary>
    /// When the person invited accepted or declined, null while it's waiting or when it was withdrawn first.
    /// </summary>
    public DateTime? AnsweredAt { get; private set; }

    /// <summary>
    /// When it was withdrawn or ended, null while it's waiting or linked.
    /// </summary>
    public DateTime? EndedAt { get; private set; }

    /// <summary>
    /// Who withdrew or ended it, null until one of them does.
    /// </summary>
    public Guid? EndedBy { get; private set; }

    /// <summary>
    /// Waiting for an answer or linked, the states only one link between two people can be in at once.
    /// </summary>
    public bool IsLive => Status is UserLinkStatus.Pending or UserLinkStatus.Accepted;

    /// <summary>
    /// Whether <paramref name="userId"/> is one of the two people.
    /// </summary>
    /// <param name="userId">The user to check.</param>
    public bool Involves(Guid userId) => userId == UserId || userId == InviteeId;

    /// <summary>
    /// Two people in a set order, the same whichever is given first, as their link is stored.
    /// </summary>
    /// <param name="oneUserId">One of the two.</param>
    /// <param name="otherUserId">The other.</param>
    public static (Guid First, Guid Second) PairOf(Guid oneUserId, Guid otherUserId)
    {
        if (oneUserId.CompareTo(otherUserId) < 0)
        {
            return (oneUserId, otherUserId);
        }

        return (otherUserId, oneUserId);
    }

    /// <summary>
    /// <paramref name="inviterId"/>, either of them, invites the other again after the invite was declined or
    /// withdrawn or the link ended. Waiting for the other to answer as a new invite.
    /// </summary>
    /// <param name="inviterId">Whichever of them is inviting.</param>
    /// <param name="now">The time now, in UTC.</param>
    /// <exception cref="InvalidOperationException"><paramref name="inviterId"/> isn't in the link, or it's waiting or
    /// linked already.</exception>
    public void InviteAgain(Guid inviterId, DateTime now)
    {
        if (!Involves(inviterId))
        {
            throw new DomainRuleException("Only the two people in a link can invite each other.");
        }

        if (IsLive)
        {
            throw new DomainRuleException("They're already invited or linked.");
        }

        InvitedBy = inviterId;
        InvitedAt = now;
        Status = UserLinkStatus.Pending;
        AnsweredAt = null;
        EndedAt = null;
        EndedBy = null;
    }

    /// <summary>
    /// The other person in the link from <paramref name="userId"/>'s side.
    /// </summary>
    /// <param name="userId">One of the two.</param>
    /// <exception cref="ArgumentException"><paramref name="userId"/> isn't in the link.</exception>
    public Guid OtherThan(Guid userId)
    {
        if (userId == UserId)
        {
            return InviteeId;
        }

        if (userId == InviteeId)
        {
            return UserId;
        }

        throw new ArgumentException("The user isn't part of this link.", nameof(userId));
    }

    /// <summary>
    /// The person invited says yes.
    /// </summary>
    /// <param name="userId">Who's accepting.</param>
    /// <param name="now">The time now, in UTC.</param>
    /// <exception cref="InvalidOperationException">Someone other than the person invited is answering, or it isn't
    /// waiting for an answer.</exception>
    public void Accept(Guid userId, DateTime now)
    {
        EnsureAnswerableBy(userId);
        Status = UserLinkStatus.Accepted;
        AnsweredAt = now;
    }

    /// <summary>
    /// The person invited says no.
    /// </summary>
    /// <param name="userId">Who's declining.</param>
    /// <param name="now">The time now, in UTC.</param>
    /// <exception cref="InvalidOperationException">Someone other than the person invited is answering, or it isn't
    /// waiting for an answer.</exception>
    public void Decline(Guid userId, DateTime now)
    {
        EnsureAnswerableBy(userId);
        Status = UserLinkStatus.Declined;
        AnsweredAt = now;
    }

    /// <summary>
    /// Either of them ends it: the inviter withdrawing an invite still waiting, or either of them ending a link they
    /// accepted. Whatever they shared stops being shared.
    /// </summary>
    /// <param name="userId">Who's ending it.</param>
    /// <param name="now">The time now, in UTC.</param>
    /// <exception cref="InvalidOperationException"><paramref name="userId"/> isn't in the link, it's already over,
    /// or the person invited is trying to withdraw rather than decline.</exception>
    public void End(Guid userId, DateTime now)
    {
        if (!Involves(userId))
        {
            throw new DomainRuleException("Only the two people in a link can end it.");
        }

        if (!IsLive)
        {
            throw new DomainRuleException("The link has already ended.");
        }

        if (Status == UserLinkStatus.Pending && userId == InvitedUserId)
        {
            throw new DomainRuleException("The person invited declines an invite rather than ending it.");
        }

        Status = UserLinkStatus.Ended;
        EndedAt = now;
        EndedBy = userId;
    }

    /// <summary>
    /// Checks <paramref name="userId"/> can answer the invite.
    /// </summary>
    /// <param name="userId">Who's answering.</param>
    /// <exception cref="DomainRuleException">They aren't the person invited, or it isn't waiting for an answer.
    /// </exception>
    private void EnsureAnswerableBy(Guid userId)
    {
        if (userId != InvitedUserId)
        {
            throw new DomainRuleException("Only the person invited can answer an invite.");
        }

        if (Status != UserLinkStatus.Pending)
        {
            throw new DomainRuleException("The invite has already been answered or withdrawn.");
        }
    }
}
