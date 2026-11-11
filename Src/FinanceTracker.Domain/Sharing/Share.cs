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

    public Share(Guid sharedBy, Guid linkId, ShareItemType itemType, Guid itemId, ShareMode mode, DateTime sharedAt)
        : base(sharedBy)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(linkId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(itemId, Guid.Empty);
        LinkId = linkId;
        ItemType = itemType;
        ItemId = itemId;
        Mode = mode;
        SharedAt = sharedAt;
    }

    /// <summary>
    /// The link it's shared through, and so who with.
    /// </summary>
    public Guid LinkId { get; private set; }

    public ShareItemType ItemType { get; private set; }

    /// <summary>
    /// The id of what's shared, a <see cref="ItemType"/>'s id.
    /// </summary>
    public Guid ItemId { get; private set; }

    public ShareMode Mode { get; private set; }

    public DateTime SharedAt { get; private set; }

    public void ChangeMode(ShareMode mode) => Mode = mode;
}
