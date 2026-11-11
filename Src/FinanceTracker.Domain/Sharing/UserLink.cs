using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Sharing;

/// <summary>
/// Two users on the instance who've agreed to share with each other. One invites, the other accepts, and either can
/// end it. Owned by the user who invited, <see cref="AggregateRoot.UserId"/>, though it belongs to both, so it's
/// looked up by <see cref="Involves"/> rather than by owner.
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
    public UserLink(Guid inviterId, Guid inviteeId, DateTime invitedAt) : base(inviterId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(inviteeId, Guid.Empty);
        if (inviteeId == inviterId)
        {
            throw new ArgumentException("A user can't link with themselves.", nameof(inviteeId));
        }

        InviteeId = inviteeId;
        InvitedAt = invitedAt;
        Status = UserLinkStatus.Pending;
    }

    /// <summary>
    /// The user who was invited.
    /// </summary>
    public Guid InviteeId { get; private set; }

    public UserLinkStatus Status { get; private set; }

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

    public bool Involves(Guid userId) => userId == UserId || userId == InviteeId;

    /// <summary>
    /// The other person in the link from <paramref name="userId"/>'s side.
    /// </summary>
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
    /// <exception cref="InvalidOperationException"><paramref name="userId"/> isn't in the link, it's already over,
    /// or the person invited is trying to withdraw rather than decline.</exception>
    public void End(Guid userId, DateTime now)
    {
        if (!Involves(userId))
        {
            throw new InvalidOperationException("Only the two people in a link can end it.");
        }

        if (!IsLive)
        {
            throw new InvalidOperationException("The link has already ended.");
        }

        if (Status == UserLinkStatus.Pending && userId == InviteeId)
        {
            throw new InvalidOperationException("The person invited declines an invite rather than ending it.");
        }

        Status = UserLinkStatus.Ended;
        EndedAt = now;
        EndedBy = userId;
    }

    private void EnsureAnswerableBy(Guid userId)
    {
        if (userId != InviteeId)
        {
            throw new InvalidOperationException("Only the person invited can answer an invite.");
        }

        if (Status != UserLinkStatus.Pending)
        {
            throw new InvalidOperationException("The invite has already been answered or withdrawn.");
        }
    }
}
