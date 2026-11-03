using System.Globalization;
using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Reports;
using FinanceTracker.Contracts.Reports.Account;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.Reports.SpentInTimePeriod;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Reports;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IReportService>]
public class ReportService : ServiceBase<ReportService>, IReportService
{
    private readonly TimeProvider _timeProvider;

    public ReportService(ICurrentUser currentUser, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        ILogger<ReportService> logger, TimeProvider timeProvider)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
    }

    public async IAsyncEnumerable<SpentInTimePeriodReport> GetSpentInTimePeriodReportAsync(
        FilterTransactions request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        (List<BankTransaction> openBankingTransactions, Dictionary<Guid, string> customTags) =
            await GetTransactionsForReportAsync(request, context, cancellationToken);

        List<ReportAmount> amounts = openBankingTransactions
            .Select(x => Matching(x, request.Tags, customTags))
            .ToList();

        if (amounts.Count == 0)
        {
            yield break;
        }

        SpentInTimePeriodReport rsp = new()
        {
            TotalIn = TotalIn(amounts), TotalOut = TotalOut(amounts), TotalTransactions = amounts.Count
        };

        foreach (IGrouping<int, ReportAmount> yearlyGrouping in amounts.GroupBy(static x => x.OccurredAt.Year))
        {
            SpentInTimePeriodReportYearlyBreakdown yearGrp = new()
            {
                Year = yearlyGrouping.Key,
                TotalIn = TotalIn(yearlyGrouping),
                TotalOut = TotalOut(yearlyGrouping),
                TotalTransactions = yearlyGrouping.Count()
            };

            foreach (IGrouping<int, ReportAmount> monthlyGrouping in yearlyGrouping.GroupBy(static x =>
                         x.OccurredAt.Month))
            {
                string monthName = DateTimeFormatInfo.CurrentInfo.GetMonthName(monthlyGrouping.Key);
                SpentInTimePeriodReportMonthlyBreakdown monthGrp = new()
                {
                    Month = monthName,
                    TotalIn = TotalIn(monthlyGrouping),
                    TotalOut = TotalOut(monthlyGrouping),
                    TotalTransactions = monthlyGrouping.Count()
                };

                foreach (IGrouping<int, ReportAmount> dayGrouping in monthlyGrouping.GroupBy(static x =>
                             x.OccurredAt.Day))
                {
                    monthGrp.DailyBreakdown.Add(new SpentInTimePeriodReportDailyBreakdown
                    {
                        Day = dayGrouping.Key,
                        TotalIn = TotalIn(dayGrouping),
                        TotalOut = TotalOut(dayGrouping),
                        TotalTransactions = dayGrouping.Count()
                    });
                }

                yearGrp.MonthlyBreakdown.Add(monthGrp);
            }

            rsp.YearlyBreakdown.Add(yearGrp);
        }

        yield return rsp;
    }

    public async IAsyncEnumerable<SpentInCategoryReport> GetCategoryBreakdownReportAsync(
        FilterTransactions request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        (List<BankTransaction> openBankingTransactions, Dictionary<Guid, string> customTags) =
            await GetTransactionsForReportAsync(request, context, cancellationToken);

        IEnumerable<string> distinctClassifications = openBankingTransactions
            .SelectMany(x => TransactionCriteria.GetTags(x, customTags)).Distinct();

        foreach (string classification in distinctClassifications)
        {
            List<ReportAmount> tagged = openBankingTransactions
                .Where(x => TransactionCriteria.GetTags(x, customTags).Contains(classification))
                .Select(x => Matching(x, [classification], customTags))
                .ToList();

            SpentInCategoryReport rsp = new()
            {
                TotalIn = TotalIn(tagged),
                TotalOut = TotalOut(tagged),
                TotalTransactions = tagged.Count,
                Category = classification
            };

            foreach (IGrouping<int, ReportAmount> yearlyGrouping in tagged.GroupBy(static x => x.OccurredAt.Year))
            {
                SpentInCategoryReportYearlyBreakdown yearGrp = new()
                {
                    Year = yearlyGrouping.Key,
                    TotalIn = TotalIn(yearlyGrouping),
                    TotalOut = TotalOut(yearlyGrouping),
                    TotalTransactions = yearlyGrouping.Count()
                };

                foreach (IGrouping<int, ReportAmount> monthlyGrouping in yearlyGrouping.GroupBy(static x =>
                             x.OccurredAt.Month))
                {
                    string monthName = DateTimeFormatInfo.CurrentInfo.GetMonthName(monthlyGrouping.Key);
                    SpentInCategoryReportMonthlyBreakdown monthGrp = new()
                    {
                        Month = monthName,
                        TotalIn = TotalIn(monthlyGrouping),
                        TotalOut = TotalOut(monthlyGrouping),
                        TotalTransactions = monthlyGrouping.Count()
                    };

                    foreach (IGrouping<int, ReportAmount> dayGrouping in monthlyGrouping.GroupBy(static x =>
                                 x.OccurredAt.Day))
                    {
                        monthGrp.DailyBreakdown.Add(new SpentInCategoryReportDailyBreakdown
                        {
                            Day = dayGrouping.Key,
                            TotalIn = TotalIn(dayGrouping),
                            TotalOut = TotalOut(dayGrouping),
                            TotalTransactions = dayGrouping.Count()
                        });
                    }

                    yearGrp.MonthlyBreakdown.Add(monthGrp);
                }

                rsp.YearlyBreakdown.Add(yearGrp);
            }

            yield return rsp;
        }
    }

    public async IAsyncEnumerable<SpentInAccountReport> GetAccountBreakdownReportAsync(
        FilterTransactions request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        (List<BankTransaction> openBankingTransactions, Dictionary<Guid, string> customTags) =
            await GetTransactionsForReportAsync(request, context, cancellationToken, includeTransfers: true);

        Dictionary<Guid, string> accountNames = await context.BankAccounts.OwnedBy(UserId)
            .ToDictionaryAsync(static x => x.Id, static x => x.DisplayName, cancellationToken);

        List<ReportAmount> amounts = openBankingTransactions
            .Select(x => Matching(x, request.Tags, customTags))
            .ToList();

        // By id, display names aren't unique across providers
        foreach (IGrouping<Guid, ReportAmount> accountGrouping in amounts.GroupBy(static x => x.AccountId))
        {
            SpentInAccountReport rsp = new()
            {
                TotalIn = TotalIn(accountGrouping),
                TotalOut = TotalOut(accountGrouping),
                TotalTransactions = accountGrouping.Count(),
                AccountId = accountGrouping.Key,
                AccountName = accountNames[accountGrouping.Key]
            };

            foreach (IGrouping<int, ReportAmount> yearlyGrouping in accountGrouping.GroupBy(static x =>
                         x.OccurredAt.Year))
            {
                SpentInAccountReportYearlyBreakdown yearGrp = new()
                {
                    Year = yearlyGrouping.Key,
                    TotalIn = TotalIn(yearlyGrouping),
                    TotalOut = TotalOut(yearlyGrouping),
                    TotalTransactions = yearlyGrouping.Count()
                };

                foreach (IGrouping<int, ReportAmount> monthlyGrouping in yearlyGrouping.GroupBy(static x =>
                             x.OccurredAt.Month))
                {
                    string monthName = DateTimeFormatInfo.CurrentInfo.GetMonthName(monthlyGrouping.Key);
                    SpentInAccountReportMonthlyBreakdown monthGrp = new()
                    {
                        Month = monthName,
                        TotalIn = TotalIn(monthlyGrouping),
                        TotalOut = TotalOut(monthlyGrouping),
                        TotalTransactions = monthlyGrouping.Count()
                    };

                    foreach (IGrouping<int, ReportAmount> dayGrouping in monthlyGrouping.GroupBy(static x =>
                                 x.OccurredAt.Day))
                    {
                        monthGrp.DailyBreakdown.Add(new SpentInAccountReportDailyBreakdown
                        {
                            Day = dayGrouping.Key,
                            TotalIn = TotalIn(dayGrouping),
                            TotalOut = TotalOut(dayGrouping),
                            TotalTransactions = dayGrouping.Count()
                        });
                    }

                    yearGrp.MonthlyBreakdown.Add(monthGrp);
                }

                rsp.YearlyBreakdown.Add(yearGrp);
            }

            yield return rsp;
        }
    }

    /// <summary>
    /// What <paramref name="transaction"/> adds to a report about <paramref name="tags"/>, all of it when there are
    /// none. The bank's classifications are on the whole of it, as are the user's tags unless it's split, then only
    /// the parts with one of them count.
    /// </summary>
    private static ReportAmount Matching(BankTransaction transaction, IReadOnlyCollection<string>? tags,
        IReadOnlyDictionary<Guid, string> customTags)
    {
        ReportAmount whole = new(transaction.AccountId, transaction.OccurredAt, transaction.Amount);
        if (tags is not { Count: > 0 } || !transaction.IsSplit ||
            transaction.VisibleProviderClassifications.Any(tags.Contains))
        {
            return whole;
        }

        decimal parts = transaction.Splits
            .Where(x => x.TagId is { } tagId && customTags.GetValueOrDefault(tagId) is { } tag && tags.Contains(tag))
            .Sum(static x => x.Amount);

        return whole with { Amount = parts };
    }

    private static decimal TotalIn(IEnumerable<ReportAmount> amounts) =>
        amounts.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount);

    private static decimal TotalOut(IEnumerable<ReportAmount> amounts) =>
        amounts.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount);

    /// <param name="includeTransfers">Whether transfers between the user's own accounts or within their household are
    /// kept, for the account breakdown, which shows an account's own money in and out. The other reports are income
    /// and spending, so leave them out.</param>
    private async Task<(List<BankTransaction> Transactions, Dictionary<Guid, string> CustomTags)>
        GetTransactionsForReportAsync(FilterTransactions request, FinanceTrackerContext context,
            CancellationToken cancellationToken, bool includeTransfers = false)
    {
        TransactionCriteria criteria = await TransactionCriteria.From(request)
            .WithSavingsGoalsAsync(context, UserId, _timeProvider.LocalTimeZone, cancellationToken);
        Dictionary<Guid, string> customTags = await context.CustomClassifications.OwnedBy(UserId)
            .ToDictionaryAsync(static x => x.Id, static x => x.Tag, cancellationToken);

        List<BankTransaction> transactions = await criteria
            .ApplyServerFilters(context.BankTransactions.OwnedBy(UserId), context)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Category and timestamps are encrypted, so filter and sort after decryption
        return (transactions
            .Where(x => includeTransfers || x.CountsAsIncomeOrSpending)
            .Where(x => criteria.MatchesClientFilters(x, customTags))
            .OrderByDescending(static x => x.OccurredAt)
            .ToList(), customTags);
    }
}
