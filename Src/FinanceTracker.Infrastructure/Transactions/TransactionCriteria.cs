using FinanceTracker.Contracts.Reports;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Infrastructure.Transactions;

/// <summary>
/// Transaction filters shared by the transaction listing and reports.
/// </summary>
internal sealed record TransactionCriteria(
    IReadOnlyCollection<Guid>? AccountIds,
    IReadOnlyCollection<Guid>? ProviderIds,
    IReadOnlyCollection<string>? Types,
    IReadOnlyCollection<string>? Categories,
    IReadOnlyCollection<string>? Tags,
    string? SearchTerm,
    DateTime? FromDate,
    DateTime? ToDate)
{
    public static TransactionCriteria From(FilterTransactions filter) => new(filter.AccountIds, filter.ProviderIds,
        filter.Types, filter.Categories, filter.Tags, filter.SearchTerm, filter.FromDate, filter.ToDate);

    public static TransactionCriteria From(BaseReport report) => new(report.AccountIds, report.ProviderIds,
        report.Types, report.Categories, report.Tags, report.SearchTerm, report.FromDate, report.ToDate);

    public IQueryable<BankTransaction> ApplyServerFilters(IQueryable<BankTransaction> query,
        FinanceTrackerContext context)
    {
        if (AccountIds is { Count: > 0 })
        {
            query = query.Where(x => AccountIds.Contains(x.AccountId));
        }

        if (ProviderIds is { Count: > 0 })
        {
            query = query.Where(x =>
                context.BankAccounts.Any(a => a.Id == x.AccountId && ProviderIds.Contains(a.ConnectionId)));
        }

        if (Types is { Count: > 0 })
        {
            List<TransactionType> types = Types
                .Select(static x => Enum.TryParse(x, true, out TransactionType type) ? type : (TransactionType?)null)
                .OfType<TransactionType>()
                .ToList();
            query = query.Where(x => types.Contains(x.Type));
        }

        if (Categories is { Count: > 0 })
        {
            query = query.Where(x => Categories.Contains(x.Category));
        }

        return query;
    }

    public bool MatchesClientFilters(BankTransaction transaction, IReadOnlyDictionary<Guid, string> customTags)
    {
        if (SearchTerm is not null &&
            !transaction.Description.Contains(SearchTerm, StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }

        if (FromDate is { } fromDate && transaction.OccurredAt < fromDate)
        {
            return false;
        }

        if (ToDate is { } toDate && transaction.OccurredAt > toDate)
        {
            return false;
        }

        return Tags is not { Count: > 0 } || GetTags(transaction, customTags).Any(Tags.Contains);
    }

    /// <summary>
    /// Provider tags followed by the names of applied custom classifications.
    /// </summary>
    public static IEnumerable<string> GetTags(BankTransaction transaction, IReadOnlyDictionary<Guid, string> customTags) =>
        transaction.ProviderClassifications.Concat(transaction.CustomClassificationIds
            .Select(id => customTags.GetValueOrDefault(id))
            .OfType<string>());
}
