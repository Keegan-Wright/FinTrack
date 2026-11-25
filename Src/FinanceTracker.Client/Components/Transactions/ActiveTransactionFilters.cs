using FinanceTracker.Contracts.Transactions;
using MudBlazor;

namespace FinanceTracker.Client.Components.Transactions;

/// <summary>
/// The filters chosen in <see cref="TransactionFilters"/>.
/// </summary>
/// <param name="ProviderIds">The banks chosen.</param>
/// <param name="AccountIds">The accounts chosen.</param>
/// <param name="Categories">The bank's categories chosen.</param>
/// <param name="TransactionTypes">Money in, money out or both.</param>
/// <param name="TransactionTags">The tags chosen.</param>
/// <param name="DateRange">The dates chosen, null for any.</param>
/// <param name="SearchTerm">The text searched for, empty for none.</param>
/// <param name="BillIds">The bills chosen.</param>
/// <param name="DebtIds">The debts chosen.</param>
/// <param name="SavingsGoalIds">The savings goals chosen.</param>
public sealed record ActiveTransactionFilters(
    IEnumerable<Guid> ProviderIds,
    IEnumerable<Guid> AccountIds,
    IEnumerable<string> Categories,
    IEnumerable<string> TransactionTypes,
    IEnumerable<string> TransactionTags,
    DateRange? DateRange,
    string SearchTerm,
    IEnumerable<Guid> BillIds,
    IEnumerable<Guid> DebtIds,
    IEnumerable<Guid> SavingsGoalIds)
{
    /// <summary>
    /// Nothing filtered, every transaction.
    /// </summary>
    public static ActiveTransactionFilters None { get; } = new([], [], [], [], [], null, string.Empty, [], [], []);

    /// <summary>
    /// The filters as the request the transaction and report services take, the dates as whole days where the user
    /// is (<paramref name="timeZone"/>).
    /// </summary>
    /// <param name="timeZone">Where the user is, for the date range's days.</param>
    public FilterTransactions ToRequest(TimeZoneInfo timeZone) => new()
    {
        AccountIds = AccountIds.ToList(),
        ProviderIds = ProviderIds.ToList(),
        Categories = Categories.ToList(),
        Types = TransactionTypes.ToList(),
        Tags = TransactionTags.ToList(),
        BillIds = BillIds.ToList(),
        DebtIds = DebtIds.ToList(),
        SavingsGoalIds = SavingsGoalIds.ToList(),
        SearchTerm = SearchTerm,
        FromDate = StartOf(DateRange?.Start, timeZone),
        // The end date is a whole day, through to its last tick.
        ToDate = EndOf(DateRange?.End, timeZone)
    };

    /// <summary>
    /// When <paramref name="day"/> starts where the user is, in UTC. Null for none.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <param name="timeZone">Where the user is.</param>
    private static DateTime? StartOf(DateTime? day, TimeZoneInfo timeZone)
    {
        if (day is not { } date)
        {
            return null;
        }

        return LocalDays.StartUtc(date, timeZone);
    }

    /// <summary>
    /// When <paramref name="day"/> ends where the user is, in UTC. Null for none.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <param name="timeZone">Where the user is.</param>
    private static DateTime? EndOf(DateTime? day, TimeZoneInfo timeZone)
    {
        if (day is not { } date)
        {
            return null;
        }

        return LocalDays.EndUtc(date, timeZone);
    }
}
