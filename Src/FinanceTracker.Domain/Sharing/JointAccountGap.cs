namespace FinanceTracker.Domain.Sharing;

/// <summary>
/// A stretch the shown copy of a <see cref="JointAccount"/> missed because its connection lapsed, filled from the
/// hidden copy. Open until the shown copy syncs again, then closed where its own transactions start again. What it
/// still lacks inside the stretch, history the bank won't release, stays filled for good.
/// </summary>
/// <param name="From">When the shown copy's last transaction before the lapse happened, the hidden copy's later ones
/// fill.</param>
/// <param name="To">When the shown copy's first transaction after the lapse happened, null while it's open.</param>
/// <param name="OpenedAt">When the lapse was found, the shown copy's balance comes from the hidden copy from that day.
/// </param>
/// <param name="ClosedAt">When the shown copy was found syncing again, null while it's open.</param>
public sealed record JointAccountGap(DateTime From, DateTime? To, DateTime OpenedAt, DateTime? ClosedAt)
{
    public bool IsOpen => ClosedAt is null;

    /// <summary>
    /// Whether a transaction at <paramref name="occurredAt"/> falls inside it, after the shown copy's last and before
    /// its first again.
    /// </summary>
    public bool Covers(DateTime occurredAt) => occurredAt > From && (To is null || occurredAt < To);

    /// <summary>
    /// Whether the shown copy's balance on <paramref name="day"/> is the hidden copy's, from the day the lapse was
    /// found until the day it synced again.
    /// </summary>
    public bool CoversBalanceOn(DateOnly day) =>
        day >= DateOnly.FromDateTime(OpenedAt) && (ClosedAt is null || day < DateOnly.FromDateTime(ClosedAt.Value));
}
