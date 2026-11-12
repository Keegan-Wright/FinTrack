using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.SavingsGoals;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Infrastructure.Sharing;
using Microsoft.EntityFrameworkCore;

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
    DateTime? ToDate,
    IReadOnlyCollection<Guid>? BillIds,
    IReadOnlyCollection<Guid>? DebtIds,
    IReadOnlyCollection<Guid>? SavingsGoalIds)
{
    /// <summary>
    /// The goals chosen in <see cref="SavingsGoalIds"/>, see <see cref="WithSavingsGoalsAsync"/>.
    /// </summary>
    private IReadOnlyList<SavingsGoal> _savingsGoals = [];

    private TimeZoneInfo _timeZone = TimeZoneInfo.Utc;

    public static TransactionCriteria From(FilterTransactions filter) => new(filter.AccountIds, filter.ProviderIds,
        filter.Types, filter.Categories, filter.Tags, filter.SearchTerm, filter.FromDate, filter.ToDate,
        filter.BillIds, filter.DebtIds, filter.SavingsGoalIds);

    /// <summary>
    /// Loads the goals chosen in <see cref="SavingsGoalIds"/>, which <see cref="MatchesClientFilters"/> needs to tell
    /// what counts towards them. Goals that aren't the user's are left out, so they match nothing.
    /// </summary>
    /// <param name="timeZone">Where the user is, for the day a tag goal tracks from.</param>
    public async Task<TransactionCriteria> WithSavingsGoalsAsync(FinanceTrackerContext context, Guid userId,
        TimeZoneInfo timeZone, CancellationToken cancellationToken)
    {
        if (SavingsGoalIds is not { Count: > 0 })
        {
            return this;
        }

        List<SavingsGoal> goals = await context.SavingsGoals.OwnedBy(userId).AsNoTracking()
            .Where(x => SavingsGoalIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        return this with { _savingsGoals = goals, _timeZone = timeZone };
    }

    public IQueryable<BankTransaction> ApplyServerFilters(IQueryable<BankTransaction> query,
        FinanceTrackerContext context)
    {
        if (AccountIds is { Count: > 0 })
        {
            query = query.Where(x => AccountIds.Contains(x.AccountId));
        }

        if (BillIds is { Count: > 0 })
        {
            query = query.Where(x => x.BillPayment != null && x.BillPayment.BillId != null &&
                                     BillIds.Contains(x.BillPayment.BillId.Value));
        }

        if (DebtIds is { Count: > 0 })
        {
            query = query.Where(x => x.DebtPayment != null && x.DebtPayment.DebtId != null &&
                                     DebtIds.Contains(x.DebtPayment.DebtId.Value));
        }

        if (ProviderIds is { Count: > 0 })
        {
            query = query.Where(x =>
                context.BankAccounts.Any(a => a.Id == x.AccountId && ProviderIds.Contains(a.ConnectionId)));
        }

        // exact range is filtered once decrypted
        return query.WhereEncryptedDayWithin(x => x.OccurredAt, FromDate, ToDate, context);
    }

    /// <summary>
    /// Filters on encrypted columns, which can only be compared once decrypted.
    /// </summary>
    /// <param name="view">What the user sees, so a search only finds a description they'd be shown, see
    /// <see cref="PrivateTransfers"/>.</param>
    public bool MatchesClientFilters(BankTransaction transaction, IReadOnlyDictionary<Guid, string> customTags,
        AccountsInView view)
    {
        if (Types is { Count: > 0 } &&
            !Types.Any(x => Enum.TryParse(x, true, out TransactionType type) && type == transaction.Type))
        {
            return false;
        }

        if (Categories is { Count: > 0 } && !Categories.Contains(transaction.Category))
        {
            return false;
        }

        if (SearchTerm is not null &&
            !view.DescriptionOf(transaction).Contains(SearchTerm, StringComparison.CurrentCultureIgnoreCase))
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

        if (SavingsGoalIds is { Count: > 0 } && !_savingsGoals.Any(x => x.Counts(transaction, _timeZone)))
        {
            return false;
        }

        return Tags is not { Count: > 0 } || GetTags(transaction, customTags).Any(Tags.Contains);
    }

    /// <summary>
    /// Provider tags the user hasn't hidden followed by the names of applied custom classifications.
    /// </summary>
    public static IEnumerable<string> GetTags(BankTransaction transaction, IReadOnlyDictionary<Guid, string> customTags) =>
        transaction.VisibleProviderClassifications.Concat(transaction.CustomClassificationIds
            .Select(id => customTags.GetValueOrDefault(id))
            .OfType<string>());
}
