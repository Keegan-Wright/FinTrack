using FinanceTracker.Domain.Debts;
using FinanceTracker.Infrastructure.Debts;
using FinanceTracker.Infrastructure.OpenBanking.Balances;
using FinanceTracker.Infrastructure.Sharing;

namespace FinanceTracker.Infrastructure.NetWorth;

/// <summary>
/// The debts other people share with the user that aren't linked to an account, which count in their Everyone net
/// worth as owed, see <see cref="DebtsOwed"/>. Worked out from their owners' payments, so they owe the same for both.
/// One linked to an account counts through that account's balance when it's shared.
/// </summary>
/// <param name="Debts">The shared debts not linked to an account.</param>
/// <param name="Owed">What they owed on any day.</param>
internal sealed record SharedDebtsOwed(IReadOnlyList<VisibleItem<Debt>> Debts, DebtsOwed Owed)
{
    public bool IsEmpty => Debts.Count == 0;

    /// <summary>
    /// What <paramref name="debt"/> owed on <paramref name="day"/>.
    /// </summary>
    public decimal OwedOn(VisibleItem<Debt> debt, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(debt);
        return DebtCalculator.OwedOn(debt.Item, Owed.Payments, Owed.TimeZone, day);
    }

    /// <summary>
    /// The shared debts in <paramref name="view"/>, none for Just mine.
    /// </summary>
    public static async Task<SharedDebtsOwed> LoadAsync(FinanceTrackerContext context, Guid userId,
        AccountsInView view, TimeZoneInfo timeZone, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.IsJustMine)
        {
            return new SharedDebtsOwed([], new DebtsOwed([], [], timeZone));
        }

        List<VisibleItem<Debt>> debts = (await SharedDebts.ForAsync(context, userId, view, cancellationToken))
            .Where(static x => !x.IsOwn && x.Item.AccountId is null)
            .ToList();
        Dictionary<Guid, DebtFigures> figures = await SharedDebts.FiguresAsync(context, view, debts,
            cancellationToken);

        return new SharedDebtsOwed(debts, new DebtsOwed(debts.Select(static x => x.Item).ToList(),
            figures.Values.SelectMany(static x => x.Payments).ToList(), timeZone));
    }
}
