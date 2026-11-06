using FinanceTracker.Client.Components.Common;
using FinanceTracker.Contracts.Transactions;
using MudBlazor;

namespace FinanceTracker.Client.Components.Transactions;

/// <summary>
/// The filters chosen in <see cref="TransactionFilters"/>.
/// </summary>
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

    private static DateTime? StartOf(DateTime? day, TimeZoneInfo timeZone)
    {
        if (day is not { } date)
        {
            return null;
        }

        return LocalDays.StartUtc(date, timeZone);
    }

    private static DateTime? EndOf(DateTime? day, TimeZoneInfo timeZone)
    {
        if (day is not { } date)
        {
            return null;
        }

        return LocalDays.EndUtc(date, timeZone);
    }
}
