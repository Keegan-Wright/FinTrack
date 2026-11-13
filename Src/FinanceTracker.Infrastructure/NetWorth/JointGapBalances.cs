using FinanceTracker.Contracts.NetWorth;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Infrastructure.Sharing;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.NetWorth;

/// <summary>
/// The balances of the joint accounts in view on the days their shown copy's connection had lapsed, taken from the
/// hidden copy, whose own connection kept it up to date, see <see cref="JointAccountGap.CoversBalanceOn"/>.
/// </summary>
internal sealed class JointGapBalances
{
    private readonly List<JointAccount> _joints;
    private readonly AccountsInView _view;
    private readonly ILookup<Guid, BalanceSnapshot> _snapshots;

    private JointGapBalances(List<JointAccount> joints, AccountsInView view, ILookup<Guid, BalanceSnapshot> snapshots)
    {
        _joints = joints;
        _view = view;
        _snapshots = snapshots;
    }

    public bool IsEmpty => _joints.Count == 0;

    /// <summary>
    /// The days the hidden copies have balances on.
    /// </summary>
    public IEnumerable<DateOnly> Days =>
        _joints.SelectMany(x => _snapshots[x.HiddenAccountId]).Select(static x => x.Date).Distinct();

    public static async Task<JointGapBalances> LoadAsync(FinanceTrackerContext context, AccountsInView view,
        CancellationToken cancellationToken)
    {
        List<JointAccount> joints = await JointAccountGaps.InViewAsync(context, view, cancellationToken);
        if (joints.Count == 0)
        {
            return new JointGapBalances([], view, Array.Empty<BalanceSnapshot>().ToLookup(static x => x.AccountId));
        }

        List<Guid> accountIds = joints.SelectMany(static x => new[] { x.ShownAccountId, x.HiddenAccountId }).ToList();
        List<BalanceSnapshot> snapshots = await context.BalanceSnapshots.AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId))
            .ToListAsync(cancellationToken);

        return new JointGapBalances(joints, view, snapshots.ToLookup(static x => x.AccountId));
    }

    /// <summary>
    /// On a day in a gap, takes the shown copy's balance as <paramref name="tally"/> counted it back out, and counts
    /// the hidden copy's in. A copy that's the user's own is counted in their own total as of
    /// <paramref name="lastOwn"/>'s day, another's as of its latest balance by <paramref name="day"/>.
    /// </summary>
    public void Adjust(NetWorthTally tally, DateOnly day, NetWorthPoint? lastOwn)
    {
        foreach (JointAccount joint in _joints.Where(x => x.BalanceFromHiddenOn(day)))
        {
            if (Counted(joint.ShownAccountId, day, lastOwn, ownTotalHasIt: _view.OwnAccountIds.Contains(joint.ShownAccountId))
                is { } shown)
            {
                tally.Remove(shown);
            }

            if (Counted(joint.HiddenAccountId, day, lastOwn, ownTotalHasIt: _view.HiddenAccountIds.Contains(joint.HiddenAccountId))
                is { } hidden)
            {
                tally.Add(hidden);
            }
        }
    }

    /// <summary>
    /// The balance <paramref name="accountId"/> shows on <paramref name="day"/>: the hidden copy's when it's the shown
    /// copy of a joint account in a gap that day, otherwise <paramref name="balance"/>.
    /// </summary>
    public decimal BalanceOn(Guid accountId, DateOnly day, decimal balance)
    {
        JointAccount? joint = _joints.Find(x => x.ShownAccountId == accountId && x.BalanceFromHiddenOn(day));
        if (joint is null)
        {
            return balance;
        }

        BalanceSnapshot? hidden = LatestBy(joint.HiddenAccountId, day);
        if (hidden is null)
        {
            return balance;
        }

        return hidden.Balance.Current;
    }

    /// <summary>
    /// <paramref name="points"/>, an account's balance history, with the days in a gap taken from the hidden copy when
    /// it's the shown copy of a joint account.
    /// </summary>
    public List<BalancePoint> History(Guid accountId, List<BalancePoint> points)
    {
        JointAccount? joint = _joints.Find(x => x.ShownAccountId == accountId);
        if (joint is null)
        {
            return points;
        }

        IEnumerable<BalancePoint> fromHidden = _snapshots[joint.HiddenAccountId]
            .Where(x => joint.BalanceFromHiddenOn(x.Date))
            .Select(NetWorthService.ToPoint);

        return points.Where(x => !joint.BalanceFromHiddenOn(x.Date))
            .Concat(fromHidden)
            .OrderBy(static x => x.Date)
            .ToList();
    }

    /// <summary>
    /// The balance of <paramref name="accountId"/> counted for <paramref name="day"/>: as of <paramref
    /// name="lastOwn"/>'s day when it's in the user's own total, otherwise its latest by <paramref name="day"/>.
    /// </summary>
    private BalanceSnapshot? Counted(Guid accountId, DateOnly day, NetWorthPoint? lastOwn, bool ownTotalHasIt)
    {
        if (!ownTotalHasIt)
        {
            return LatestBy(accountId, day);
        }

        if (lastOwn is null)
        {
            return null;
        }

        return _snapshots[accountId].FirstOrDefault(x => x.Date == lastOwn.Date);
    }

    private BalanceSnapshot? LatestBy(Guid accountId, DateOnly day) =>
        _snapshots[accountId].Where(x => x.Date <= day).MaxBy(static x => x.Date);
}
