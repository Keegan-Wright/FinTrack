using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Sharing;

/// <summary>
/// One account two linked people have each connected, a joint account, counted once. Both copies keep syncing from
/// their own connections. The first connected is shown to both of them, and is the one both annotate. The other is
/// hidden, its owner sees the shown one as theirs to change too. Owned by whoever merged them, <see
/// cref="AggregateRoot.UserId"/>, either of the two. While the shown copy's connection has lapsed, the hidden copy
/// fills the <see cref="Gaps"/>.
/// </summary>
public sealed class JointAccount : AggregateRoot
{
    private readonly List<JointAccountGap> _gaps = [];

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

    /// <summary>
    /// The stretches the shown copy missed while its connection lapsed, oldest first, the last open while it still has.
    /// </summary>
    public IReadOnlyList<JointAccountGap> Gaps => _gaps.AsReadOnly();

    public bool HasOpenGap => _gaps.Count > 0 && _gaps[^1].IsOpen;

    /// <summary>
    /// The shown copy's connection has lapsed, so the hidden copy's transactions after <paramref name="from"/> fill
    /// in for it.
    /// </summary>
    /// <param name="from">When the shown copy's last transaction happened, the earliest time there is when it has
    /// none.</param>
    /// <param name="foundAt">When the lapse was found.</param>
    /// <exception cref="InvalidOperationException">A gap is open already.</exception>
    public void OpenGap(DateTime from, DateTime foundAt)
    {
        if (HasOpenGap)
        {
            throw new InvalidOperationException("The joint account already has an open gap.");
        }

        _gaps.Add(new JointAccountGap(from, null, foundAt, null));
    }

    /// <summary>
    /// The shown copy syncs again, its own transactions taking over from <paramref name="to"/>.
    /// </summary>
    /// <param name="to">When its first transaction after the gap opened happened.</param>
    /// <param name="syncedAt">When it was found syncing again.</param>
    /// <exception cref="InvalidOperationException">No gap is open.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> isn't after the gap's start.</exception>
    public void CloseGap(DateTime to, DateTime syncedAt)
    {
        if (!HasOpenGap)
        {
            throw new InvalidOperationException("The joint account has no open gap.");
        }

        JointAccountGap open = _gaps[^1];
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(to, open.From);
        _gaps[^1] = open with { To = to, ClosedAt = syncedAt };
    }

    /// <summary>
    /// Whether a hidden copy's transaction at <paramref name="occurredAt"/> falls in one of the gaps.
    /// </summary>
    public bool InGap(DateTime occurredAt) => _gaps.Exists(x => x.Covers(occurredAt));

    /// <summary>
    /// Whether the shown copy's balance on <paramref name="day"/> is taken from the hidden copy.
    /// </summary>
    public bool BalanceFromHiddenOn(DateOnly day) => _gaps.Exists(x => x.CoversBalanceOn(day));
}
