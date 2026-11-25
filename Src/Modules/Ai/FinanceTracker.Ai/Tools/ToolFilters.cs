using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Ai.Tools;

/// <summary>
/// Turns a tool's arguments into the services' transaction filter.
/// </summary>
internal static class ToolFilters
{
    /// <summary>
    /// A filter for whole days where the install is, from the start of <paramref name="from"/> to the end of
    /// <paramref name="to"/>.
    /// </summary>
    /// <param name="timeProvider">The clock, whose time zone the days are in.</param>
    /// <param name="from">The first day, null for no start.</param>
    /// <param name="to">The last day, null up to today.</param>
    /// <param name="accountIds">Only these accounts, null or empty for all.</param>
    /// <param name="tags">Only transactions with one of these tags, null or empty for all.</param>
    /// <param name="text">Only transactions whose text contains this, null for all.</param>
    public static FilterTransactions For(TimeProvider timeProvider, DateOnly? from, DateOnly? to,
        IReadOnlyList<Guid>? accountIds = null, IReadOnlyList<string>? tags = null, string? text = null)
    {
        TimeZoneInfo timeZone = timeProvider.LocalTimeZone;
        DateTime? fromUtc = null;
        if (from is { } firstDay)
        {
            fromUtc = LocalDays.StartUtc(firstDay.ToDateTime(TimeOnly.MinValue), timeZone);
        }

        DateTime? toUtc = null;
        if (to is { } lastDay)
        {
            toUtc = LocalDays.EndUtc(lastDay.ToDateTime(TimeOnly.MinValue), timeZone);
        }

        return new FilterTransactions
        {
            FromDate = fromUtc,
            ToDate = toUtc,
            AccountIds = accountIds ?? [],
            Tags = tags ?? [],
            SearchTerm = string.IsNullOrWhiteSpace(text) ? null : text
        };
    }
}
