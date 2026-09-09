using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using FinanceTracker.Contracts.Reports;
using FinanceTracker.Contracts.Reports.Account;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.Reports.SpentInTimePeriod;
using FinanceTracker.Domain;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.OpenBanking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Reports;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<IReportService>]
public class ReportService : ServiceBase<ReportService>, IReportService
{
    private readonly IOpenBankingService _openBankingService;

    public ReportService(ClaimsPrincipal user, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        IOpenBankingService openBankingService, ILogger<ReportService> logger) : base(user, financeTrackerContextFactory, logger) =>
        _openBankingService = openBankingService;

    public async IAsyncEnumerable<SpentInTimePeriodReport> GetSpentInTimePeriodReportAsync(
        BaseReport request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<OpenBankingTransaction> query = GetQueryByBaseReportRequest(request, context);

        List<OpenBankingTransaction> openBankingTransactions = [];

        await foreach (OpenBankingTransaction transaction in query.OrderByDescending(static x => x.TransactionTime)
                           .ToAsyncEnumerable().WithCancellation(cancellationToken))
        {
            openBankingTransactions.Add(transaction);
        }

        decimal totalIn = openBankingTransactions.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount);
        decimal totalOut = openBankingTransactions.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount);
        int totalTransactions = openBankingTransactions.Count;


        foreach (IGrouping<int, OpenBankingTransaction> yearlyGrouping in openBankingTransactions.GroupBy(static x =>
                     x.TransactionTime.Year))
        {
            SpentInTimePeriodReport rsp = new()
            {
                TotalIn = totalIn, TotalOut = totalOut, TotalTransactions = totalTransactions
            };

            SpentInTimePeriodReportYearlyBreakdown yearGrp = new()
            {
                Year = yearlyGrouping.Key,
                TotalIn = yearlyGrouping.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                TotalOut = yearlyGrouping.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                TotalTransactions = yearlyGrouping.Count()
            };

            foreach (IGrouping<int, OpenBankingTransaction> monthlyGrouping in yearlyGrouping.GroupBy(static x =>
                         x.TransactionTime.Month))
            {
                string monthName = DateTimeFormatInfo.CurrentInfo.GetMonthName(monthlyGrouping.Key);
                SpentInTimePeriodReportMonthlyBreakdown monthGrp = new()
                {
                    Month = monthName,
                    TotalIn = monthlyGrouping.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                    TotalOut = monthlyGrouping.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                    TotalTransactions = monthlyGrouping.Count()
                };


                IEnumerable<IGrouping<int, OpenBankingTransaction>> dayGrp =
                    monthlyGrouping.GroupBy(static x => x.TransactionTime.Day);
                foreach (IGrouping<int, OpenBankingTransaction> dayGrouping in dayGrp)
                {
                    monthGrp.DailyBreakdown.Add(new SpentInTimePeriodReportDailyBreakdown
                    {
                        Day = dayGrouping.Key,
                        TotalIn = dayGrouping.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                        TotalOut = dayGrouping.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                        TotalTransactions = dayGrouping.Count()
                    });
                }

                yearGrp.MonthlyBreakdown.Add(monthGrp);
            }

            rsp.YearlyBreakdown.Add(yearGrp);
            yield return rsp;
        }
    }

    public async IAsyncEnumerable<SpentInCategoryReport> GetCategoryBreakdownReportAsync(
        BaseReport request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<OpenBankingTransaction> query = GetQueryByBaseReportRequest(request, context);

        List<OpenBankingTransaction> openBankingTransactions = [];

        await foreach (OpenBankingTransaction transaction in query.OrderByDescending(static x => x.TransactionTime)
                           .ToAsyncEnumerable().WithCancellation(cancellationToken))
        {
            openBankingTransactions.Add(transaction);
        }

        IEnumerable<string> distinctClassifications = openBankingTransactions
            .SelectMany(static x => x.Classifications!.Select(static c => c.Classification)).Distinct();


        foreach (string classification in distinctClassifications)
        {
            IEnumerable<OpenBankingTransaction> transactions =
                openBankingTransactions.Where(x => x.Classifications!.Any(c => c.Classification == classification));

            IEnumerable<OpenBankingTransaction> bankingTransactions = transactions.ToList();
            decimal totalIn = bankingTransactions.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount);
            decimal totalOut = bankingTransactions.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount);
            int totalTransactions = bankingTransactions.Count();


            SpentInCategoryReport rsp = new()
            {
                TotalIn = totalIn,
                TotalOut = totalOut,
                TotalTransactions = totalTransactions,
                Category = classification
            };

            foreach (IGrouping<int, OpenBankingTransaction> yearlyGrouping in bankingTransactions.GroupBy(static x =>
                         x.TransactionTime.Year))
            {
                SpentInCategoryReportYearlyBreakdown yearGrp = new()
                {
                    Year = yearlyGrouping.Key,
                    TotalIn = yearlyGrouping.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                    TotalOut = yearlyGrouping.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                    TotalTransactions = yearlyGrouping.Count()
                };

                foreach (IGrouping<int, OpenBankingTransaction> monthlyGrouping in yearlyGrouping.GroupBy(static x =>
                             x.TransactionTime.Month))
                {
                    string monthName = DateTimeFormatInfo.CurrentInfo.GetMonthName(monthlyGrouping.Key);
                    SpentInCategoryReportMonthlyBreakdown monthGrp = new()
                    {
                        Month = monthName,
                        TotalIn = monthlyGrouping.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                        TotalOut = monthlyGrouping.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                        TotalTransactions = monthlyGrouping.Count()
                    };


                    IEnumerable<IGrouping<int, OpenBankingTransaction>> dayGrp =
                        monthlyGrouping.GroupBy(static x => x.TransactionTime.Day);
                    foreach (IGrouping<int, OpenBankingTransaction> dayGrouping in dayGrp)
                    {
                        monthGrp.DailyBreakdown.Add(new SpentInCategoryReportDailyBreakdown
                        {
                            Day = dayGrouping.Key,
                            TotalIn = dayGrouping.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                            TotalOut = dayGrouping.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                            TotalTransactions = dayGrouping.Count()
                        });
                    }

                    yearGrp.MonthlyBreakdown.Add(monthGrp);
                }

                rsp.YearlyBreakdown.Add(yearGrp);
                yield return rsp;
            }
        }
    }

    public async IAsyncEnumerable<SpentInAccountReport> GetAccountBreakdownReportAsync(
        BaseReport request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<OpenBankingTransaction> query = GetQueryByBaseReportRequest(request, context);

        List<OpenBankingTransaction> openBankingTransactions = [];

        await foreach (OpenBankingTransaction transaction in query.OrderByDescending(static x => x.TransactionTime)
                           .ToAsyncEnumerable().WithCancellation(cancellationToken))
        {
            bool blocked = BlockedByClientFilters(request, transaction);
            if (blocked)
            {
                continue;
            }

            openBankingTransactions.Add(transaction);
        }

        foreach (IGrouping<string, OpenBankingTransaction> accountGrouping in openBankingTransactions.GroupBy(static x =>
                     x.Account!.DisplayName))
        {
            decimal totalIn = accountGrouping.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount);
            decimal totalOut = accountGrouping.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount);
            int totalTransactions = accountGrouping.Count();


            SpentInAccountReport rsp = new()
            {
                TotalIn = totalIn,
                TotalOut = totalOut,
                TotalTransactions = totalTransactions,
                AccountName = (accountGrouping.FirstOrDefault()?.Account!).DisplayName };

            foreach (IGrouping<int, OpenBankingTransaction> yearlyGrouping in accountGrouping.GroupBy(static x =>
                         x.TransactionTime.Year))
            {
                SpentInAccountReportYearlyBreakdown yearGrp = new()
                {
                    Year = yearlyGrouping.Key,
                    TotalIn = yearlyGrouping.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                    TotalOut = yearlyGrouping.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                    TotalTransactions = yearlyGrouping.Count()
                };

                foreach (IGrouping<int, OpenBankingTransaction> monthlyGrouping in yearlyGrouping.GroupBy(static x =>
                             x.TransactionTime.Month))
                {
                    string monthName = DateTimeFormatInfo.CurrentInfo.GetMonthName(monthlyGrouping.Key);
                    SpentInAccountReportMonthlyBreakdown monthGrp = new()
                    {
                        Month = monthName,
                        TotalIn = monthlyGrouping.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                        TotalOut = monthlyGrouping.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                        TotalTransactions = monthlyGrouping.Count()
                    };


                    IEnumerable<IGrouping<int, OpenBankingTransaction>> dayGrp =
                        monthlyGrouping.GroupBy(static x => x.TransactionTime.Day);
                    foreach (IGrouping<int, OpenBankingTransaction> dayGrouping in dayGrp)
                    {
                        monthGrp.DailyBreakdown.Add(new SpentInAccountReportDailyBreakdown
                        {
                            Day = dayGrouping.Key,
                            TotalIn = dayGrouping.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                            TotalOut = dayGrouping.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                            TotalTransactions = dayGrouping.Count()
                        });
                    }

                    yearGrp.MonthlyBreakdown.Add(monthGrp);
                }

                rsp.YearlyBreakdown.Add(yearGrp);
                yield return rsp;
            }
        }
    }


    private IQueryable<OpenBankingTransaction> GetQueryByBaseReportRequest(BaseReport request,
        FinanceTrackerContext context)
    {
        IQueryable<OpenBankingTransaction> query = context.IsolateToUser(UserId)
            .Include(x => x.Providers)!
            .ThenInclude(x => x.Accounts)!
            .ThenInclude(x => x.Transactions)
            .SelectMany(x => x.Providers!.SelectMany(c => c.Transactions!))
            .Include(x => x.Account)
            .Include(x => x.Classifications)
            .AsNoTracking();


        if (request.AccountIds is not null && request.AccountIds.Any())
        {
            query = query.Where(x => request.AccountIds.Contains(x.Account!.Id));
        }

        if (request.Types is not null && request.Types.Any())
        {
            query = query.Where(x => request.Types.Contains(x.TransactionType));
        }

        if (request.Categories is not null && request.Categories.Any())
        {
            query = query.Where(x => request.Categories.Contains(x.TransactionCategory));
        }

        if (request.Tags is not null && request.Tags.Any())
        {
            query = query.Where(x => request.Tags.Any(y => x.Classifications!.Any(c => c.Classification == y)));
        }

        if (request.ProviderIds is not null && request.ProviderIds.Any())
        {
            query = query.Where(x => request.ProviderIds.Contains(x.Provider!.Id))
                .Select(x => x);
        }

        query = query.Where(x => x.TransactionCategory != "TRANSFER");


        return query;
    }

    private static bool BlockedByClientFilters(BaseReport request, OpenBankingTransaction transaction)
    {
        // Few Client filters due to encryption limiting ability
        if (request.SearchTerm is not null)
        {
            bool containsSearchTerm = transaction.Description.Contains(request.SearchTerm, StringComparison.CurrentCultureIgnoreCase);
            if (!containsSearchTerm)
            {
                return true;
            }
        }


        if (request.FromDate is not null)
        {
            bool containsFromDate = transaction.TransactionTime >= request.FromDate;
            if (!containsFromDate)
            {
                return true;
            }
        }

        if (request.ToDate is null)
        {
            return false;
        }

        bool containToDate = transaction.TransactionTime <= request.ToDate;
        return !containToDate;
    }
}
