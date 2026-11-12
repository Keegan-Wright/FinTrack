using System.Globalization;
using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Reports;
using FinanceTracker.Contracts.Reports.Account;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.Reports.SpentInTimePeriod;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.PayPeriods;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Infrastructure.PayPeriods;
using FinanceTracker.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Reports;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IReportService>]
public class ReportService : ServiceBase<ReportService>, IReportService
{
    private readonly TimeProvider _timeProvider;

    private readonly IAccountScope _accountScope;

    public ReportService(ICurrentUser currentUser, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        ILogger<ReportService> logger, TimeProvider timeProvider, IAccountScope accountScope)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _accountScope = accountScope;
    }

    public async IAsyncEnumerable<SpentInTimePeriodReport> GetSpentInTimePeriodReportAsync(
        FilterTransactions request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        (List<BankTransaction> openBankingTransactions, Dictionary<Guid, string> customTags) =
            await GetTransactionsForReportAsync(request, context, cancellationToken);
        PayCalendar calendar =
            await PayCalendars.ForUserAsync(context, UserId, _timeProvider.Today, cancellationToken);

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

        foreach (ReportYear year in ReportPeriods.Group(amounts, calendar, _timeProvider.LocalTimeZone))
        {
            SpentInTimePeriodReportYearlyBreakdown yearGrp = new()
            {
                Year = year.Year,
                TotalIn = TotalIn(year.Amounts),
                TotalOut = TotalOut(year.Amounts),
                TotalTransactions = year.Amounts.Count
            };

            foreach (ReportMonth month in year.Months)
            {
                SpentInTimePeriodReportMonthlyBreakdown monthGrp = new()
                {
                    Month = DateTimeFormatInfo.CurrentInfo.GetMonthName(month.Month),
                    Start = month.Period.Start,
                    End = month.Period.End,
                    TotalIn = TotalIn(month.Amounts),
                    TotalOut = TotalOut(month.Amounts),
                    TotalTransactions = month.Amounts.Count
                };

                foreach (ReportDay day in month.Days)
                {
                    monthGrp.DailyBreakdown.Add(new SpentInTimePeriodReportDailyBreakdown
                    {
                        Day = day.Date.Day,
                        Date = day.Date,
                        TotalIn = TotalIn(day.Amounts),
                        TotalOut = TotalOut(day.Amounts),
                        TotalTransactions = day.Amounts.Count
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
        PayCalendar calendar =
            await PayCalendars.ForUserAsync(context, UserId, _timeProvider.Today, cancellationToken);

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

            foreach (ReportYear year in ReportPeriods.Group(tagged, calendar, _timeProvider.LocalTimeZone))
            {
                SpentInCategoryReportYearlyBreakdown yearGrp = new()
                {
                    Year = year.Year,
                    TotalIn = TotalIn(year.Amounts),
                    TotalOut = TotalOut(year.Amounts),
                    TotalTransactions = year.Amounts.Count
                };

                foreach (ReportMonth month in year.Months)
                {
                    SpentInCategoryReportMonthlyBreakdown monthGrp = new()
                    {
                        Month = DateTimeFormatInfo.CurrentInfo.GetMonthName(month.Month),
                        Start = month.Period.Start,
                        End = month.Period.End,
                        TotalIn = TotalIn(month.Amounts),
                        TotalOut = TotalOut(month.Amounts),
                        TotalTransactions = month.Amounts.Count
                    };

                    foreach (ReportDay day in month.Days)
                    {
                        monthGrp.DailyBreakdown.Add(new SpentInCategoryReportDailyBreakdown
                        {
                            Day = day.Date.Day,
                            Date = day.Date,
                            TotalIn = TotalIn(day.Amounts),
                            TotalOut = TotalOut(day.Amounts),
                            TotalTransactions = day.Amounts.Count
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
        PayCalendar calendar =
            await PayCalendars.ForUserAsync(context, UserId, _timeProvider.Today, cancellationToken);

        AccountsInView view = await _accountScope.ResolveAsync(context, cancellationToken);
        Dictionary<Guid, string> accountNames = await context.BankAccounts.InView(view)
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

            IReadOnlyList<ReportYear> years =
                ReportPeriods.Group(accountGrouping, calendar, _timeProvider.LocalTimeZone);
            foreach (ReportYear year in years)
            {
                SpentInAccountReportYearlyBreakdown yearGrp = new()
                {
                    Year = year.Year,
                    TotalIn = TotalIn(year.Amounts),
                    TotalOut = TotalOut(year.Amounts),
                    TotalTransactions = year.Amounts.Count
                };

                foreach (ReportMonth month in year.Months)
                {
                    SpentInAccountReportMonthlyBreakdown monthGrp = new()
                    {
                        Month = DateTimeFormatInfo.CurrentInfo.GetMonthName(month.Month),
                        Start = month.Period.Start,
                        End = month.Period.End,
                        TotalIn = TotalIn(month.Amounts),
                        TotalOut = TotalOut(month.Amounts),
                        TotalTransactions = month.Amounts.Count
                    };

                    foreach (ReportDay day in month.Days)
                    {
                        monthGrp.DailyBreakdown.Add(new SpentInAccountReportDailyBreakdown
                        {
                            Day = day.Date.Day,
                            Date = day.Date,
                            TotalIn = TotalIn(day.Amounts),
                            TotalOut = TotalOut(day.Amounts),
                            TotalTransactions = day.Amounts.Count
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
        AccountsInView view = await _accountScope.ResolveAsync(context, cancellationToken);
        TransactionCriteria criteria = await TransactionCriteria.From(request)
            .WithSavingsGoalsAsync(context, UserId, _timeProvider.LocalTimeZone, cancellationToken);

        // A shared transaction's tags are its owner's, so theirs are named too
        Dictionary<Guid, string> customTags = await TagNames.LoadAsync(context, view, cancellationToken);

        List<BankTransaction> transactions = await criteria
            .ApplyServerFilters(context.BankTransactions.InView(view), context)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Category and timestamps are encrypted, so filter and sort after decryption
        return (transactions
            .Where(x => includeTransfers || x.CountsAsIncomeOrSpending)
            .Where(x => criteria.MatchesClientFilters(x, customTags, view))
            .OrderByDescending(static x => x.OccurredAt)
            .ToList(), customTags);
    }
}
