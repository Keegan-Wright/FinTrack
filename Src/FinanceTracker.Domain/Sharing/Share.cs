using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Sharing;

/// <summary>
/// One thing a user shares with the other person in one of their links, such as a bank account. Owned by the user
/// sharing it, <see cref="AggregateRoot.UserId"/>. It only counts while the link is accepted, and goes when the link
/// ends, so linking again never shares anything without them choosing it again.
/// </summary>
public sealed class Share : AggregateRoot
{
    [Obsolete("EF Use Only")]
    private Share()
    {
    }

    /// <param name="sharedBy">The user sharing it, who owns it.</param>
    /// <param name="linkId">The link it's shared through.</param>
    /// <param name="itemType">What kind of thing is shared.</param>
    /// <param name="itemId">The id of what's shared.</param>
    /// <param name="mode">Whether the other person can change it.</param>
    /// <param name="sharedAt">When it was shared.</param>
    /// <param name="reason">Why it's shared. One that follows a transaction is read only.</param>
    /// <exception cref="ArgumentException">One that follows a transaction allows changes.</exception>
    public Share(Guid sharedBy, Guid linkId, ShareItemType itemType, Guid itemId, ShareMode mode, DateTime sharedAt,
        ShareReason reason = ShareReason.Chosen)
        : base(sharedBy)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(linkId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(itemId, Guid.Empty);
        if (reason == ShareReason.OnTransaction && mode != ShareMode.ReadOnly)
        {
            throw new ArgumentException("What's shared for being on a transaction is read only.", nameof(mode));
        }

        LinkId = linkId;
        ItemType = itemType;
        ItemId = itemId;
        Mode = mode;
        SharedAt = sharedAt;
        Reason = reason;
    }

    /// <summary>
    /// The link it's shared through, and so who with.
    /// </summary>
    public Guid LinkId { get; private set; }

    /// <summary>
    /// What kind of thing is shared.
    /// </summary>
    public ShareItemType ItemType { get; private set; }

    /// <summary>
    /// The id of what's shared, a <see cref="ItemType"/>'s id.
    /// </summary>
    public Guid ItemId { get; private set; }

    /// <summary>
    /// Whether the other person can change it.
    /// </summary>
    public ShareMode Mode { get; private set; }

    /// <summary>
    /// When it was shared.
    /// </summary>
    public DateTime SharedAt { get; private set; }

    /// <summary>
    /// Why it's shared, chosen or because it's on a shared transaction.
    /// </summary>
    public ShareReason Reason { get; private set; }

    /// <param name="mode">Whether the other person can change it.</param>
    /// <exception cref="InvalidOperationException">It follows a transaction, so is read only.</exception>
    public void ChangeMode(ShareMode mode)
    {
        if (Reason == ShareReason.OnTransaction && mode != ShareMode.ReadOnly)
        {
            throw new DomainRuleException("What's shared for being on a transaction is read only.");
        }

        Mode = mode;
    }
}
