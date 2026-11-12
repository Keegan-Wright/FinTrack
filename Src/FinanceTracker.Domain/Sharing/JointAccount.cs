using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Sharing;

/// <summary>
/// One account two linked people have each connected, a joint account, counted once. Both copies keep syncing from
/// their own connections. The first connected is shown to both of them, and is the one both annotate. The other is
/// hidden, its owner sees the shown one as theirs to change too. Owned by whoever merged them, <see
/// cref="AggregateRoot.UserId"/>, either of the two.
/// </summary>
public sealed class JointAccount : AggregateRoot
{
    [Obsolete("EF Use Only")]
    private JointAccount()
    {
    }

    public JointAccount(Guid mergedBy, Guid linkId, Guid shownAccountId, Guid hiddenAccountId, DateTime mergedAt)
        : base(mergedBy)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(linkId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(shownAccountId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(hiddenAccountId, Guid.Empty);
        if (shownAccountId == hiddenAccountId)
        {
            throw new ArgumentException("An account can't be merged with itself.", nameof(hiddenAccountId));
        }

        LinkId = linkId;
        ShownAccountId = shownAccountId;
        HiddenAccountId = hiddenAccountId;
        MergedAt = mergedAt;
    }

    /// <summary>
    /// The link between the two people, it's split again when that ends.
    /// </summary>
    public Guid LinkId { get; private set; }

    /// <summary>
    /// The copy both of them see and annotate.
    /// </summary>
    public Guid ShownAccountId { get; private set; }

    /// <summary>
    /// The other person's copy, still syncing, left out of every view while they're merged.
    /// </summary>
    public Guid HiddenAccountId { get; private set; }

    public DateTime MergedAt { get; private set; }
}
