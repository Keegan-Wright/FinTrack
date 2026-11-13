using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Client.Components.Transactions;

/// <summary>
/// The tags, bills and debts that can go on a transaction. On another person's, only the user's own and that person's,
/// as a third person's would tell them of a transaction they can't see.
/// </summary>
public static class TransactionChoices
{
    /// <summary>
    /// Those of <paramref name="items"/> that can go on <paramref name="transaction"/>.
    /// </summary>
    /// <param name="sharedBy">Whose each is, null for the user's own, as the lists say it.</param>
    public static List<T> For<T>(IEnumerable<T> items, Func<T, string?> sharedBy, Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(sharedBy);
        ArgumentNullException.ThrowIfNull(transaction);

        // Their own transaction takes anything they can see
        if (transaction.SharedBy is not { } owner)
        {
            return items.ToList();
        }

        return items.Where(x => sharedBy(x) is not { } itemOwner || itemOwner == owner).ToList();
    }
}
