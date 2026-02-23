using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using FinanceTracker.Data;
using FinanceTracker.Data.Models;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Models.Request.Reports;
using FinanceTracker.Models.Response.Reports.Account;
using FinanceTracker.Models.Response.Reports.Category;
using FinanceTracker.Models.Response.Reports.SpentInTimePeriod;
using FinanceTracker.Services.OpenBanking;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Services.Reports;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<IReportService>]
public class ReportService : ServiceBase, IReportService
{
    private readonly IOpenBankingService _openBankingService;

    public ReportService(ClaimsPrincipal user, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        IOpenBankingService openBankingService) : base(user, financeTrackerContextFactory) =>
        _openBankingService = openBankingService;

    public async IAsyncEnumerable<SpentInTimePeriodReportResponse> GetSpentInTimePeriodReportAsync(
        BaseReportRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await _openBankingService.PerformSyncAsync(request.SyncTypes, cancellationToken);
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<OpenBankingTransaction> query = GetQueryByBaseReportRequest(request, context);

        List<OpenBankingTransaction> openBankingTransactions = [];

        await foreach (OpenBankingTransaction transaction in query.OrderByDescending(x => x.TransactionTime)
                           .ToAsyncEnumerable().WithCancellation(cancellationToken))
        {
            openBankingTransactions.Add(transaction);
        }

        decimal totalIn = openBankingTransactions.Where(x => !decimal.IsNegative(x.Amount)).Sum(x => x.Amount);
        decimal totalOut = openBankingTransactions.Where(x => decimal.IsNegative(x.Amount)).Sum(x => x.Amount);
        int totalTransactions = openBankingTransactions.Count;


        foreach (IGrouping<int, OpenBankingTransaction> yearlyGrouping in openBankingTransactions.GroupBy(x =>
                     x.TransactionTime.Year))
        {
            SpentInTimePeriodReportResponse rsp = new()
            {
                TotalIn = totalIn, TotalOut = totalOut, TotalTransactions = totalTransactions
            };

            SpentInTimePeriodReportYearlyBreakdownResponse yearGrp = new()
            {
                Year = yearlyGrouping.Key,
                TotalIn = yearlyGrouping.Where(x => !decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                TotalOut = yearlyGrouping.Where(x => decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                TotalTransactions = yearlyGrouping.Count()
            };

            foreach (IGrouping<int, OpenBankingTransaction> monthlyGrouping in yearlyGrouping.GroupBy(x =>
                         x.TransactionTime.Month))
            {
                string monthName = DateTimeFormatInfo.CurrentInfo.GetMonthName(monthlyGrouping.Key);
                SpentInTimePeriodReportMonthlyBreakdownResponse monthGrp = new()
                {
                    Month = monthName,
                    TotalIn = monthlyGrouping.Where(x => !decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                    TotalOut = monthlyGrouping.Where(x => decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                    TotalTransactions = monthlyGrouping.Count()
                };


                IEnumerable<IGrouping<int, OpenBankingTransaction>> dayGrp =
                    monthlyGrouping.GroupBy(x => x.TransactionTime.Day);
                foreach (IGrouping<int, OpenBankingTransaction> dayGrouping in dayGrp)
                {
                    monthGrp.DailyBreakdown.Add(new SpentInTimePeriodReportDailyBreakdownResponse
                    {
                        Day = dayGrouping.Key,
                        TotalIn = dayGrouping.Where(x => !decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                        TotalOut = dayGrouping.Where(x => decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                        TotalTransactions = dayGrouping.Count()
                    });
                }

                yearGrp.MonthlyBreakdown.Add(monthGrp);
            }

            rsp.YearlyBreakdown.Add(yearGrp);
            yield return rsp;
        }
    }

    public async IAsyncEnumerable<SpentInCategoryReportResponse> GetCategoryBreakdownReportAsync(
        BaseReportRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await _openBankingService.PerformSyncAsync(request.SyncTypes, cancellationToken);
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<OpenBankingTransaction> query = GetQueryByBaseReportRequest(request, context);

        List<OpenBankingTransaction> openBankingTransactions = [];

        await foreach (OpenBankingTransaction transaction in query.OrderByDescending(x => x.TransactionTime)
                           .ToAsyncEnumerable().WithCancellation(cancellationToken))
        {
            openBankingTransactions.Add(transaction);
        }

        IEnumerable<string> distinctClassifications = openBankingTransactions
            .SelectMany(x => x.Classifications!.Select(c => c.Classification)).Distinct();


        foreach (string classification in distinctClassifications)
        {
            IEnumerable<OpenBankingTransaction> transactions =
                openBankingTransactions.Where(x => x.Classifications!.Any(c => c.Classification == classification));

            IEnumerable<OpenBankingTransaction> bankingTransactions = transactions.ToList();
            decimal totalIn = bankingTransactions.Where(x => !decimal.IsNegative(x.Amount)).Sum(x => x.Amount);
            decimal totalOut = bankingTransactions.Where(x => decimal.IsNegative(x.Amount)).Sum(x => x.Amount);
            int totalTransactions = bankingTransactions.Count();


            SpentInCategoryReportResponse rsp = new()
            {
                TotalIn = totalIn,
                TotalOut = totalOut,
                TotalTransactions = totalTransactions,
                Category = classification
            };

            foreach (IGrouping<int, OpenBankingTransaction> yearlyGrouping in bankingTransactions.GroupBy(x =>
                         x.TransactionTime.Year))
            {
                SpentInCategoryReportYearlyBreakdownResponse yearGrp = new()
                {
                    Year = yearlyGrouping.Key,
                    TotalIn = yearlyGrouping.Where(x => !decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                    TotalOut = yearlyGrouping.Where(x => decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                    TotalTransactions = yearlyGrouping.Count()
                };

                foreach (IGrouping<int, OpenBankingTransaction> monthlyGrouping in yearlyGrouping.GroupBy(x =>
                             x.TransactionTime.Month))
                {
                    string monthName = DateTimeFormatInfo.CurrentInfo.GetMonthName(monthlyGrouping.Key);
                    SpentInCategoryReportMonthlyBreakdownResponse monthGrp = new()
                    {
                        Month = monthName,
                        TotalIn = monthlyGrouping.Where(x => !decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                        TotalOut = monthlyGrouping.Where(x => decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                        TotalTransactions = monthlyGrouping.Count()
                    };


                    IEnumerable<IGrouping<int, OpenBankingTransaction>> dayGrp =
                        monthlyGrouping.GroupBy(x => x.TransactionTime.Day);
                    foreach (IGrouping<int, OpenBankingTransaction> dayGrouping in dayGrp)
                    {
                        monthGrp.DailyBreakdown.Add(new SpentInCategoryReportDailyBreakdownResponse
                        {
                            Day = dayGrouping.Key,
                            TotalIn = dayGrouping.Where(x => !decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                            TotalOut = dayGrouping.Where(x => decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
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

    public async IAsyncEnumerable<SpentInAccountReportResponse> GetAccountBreakdownReportAsync(
        BaseReportRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await _openBankingService.PerformSyncAsync(request.SyncTypes, cancellationToken);
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<OpenBankingTransaction> query = GetQueryByBaseReportRequest(request, context);

        List<OpenBankingTransaction> openBankingTransactions = [];

        await foreach (OpenBankingTransaction transaction in query.OrderByDescending(x => x.TransactionTime)
                           .ToAsyncEnumerable().WithCancellation(cancellationToken))
        {
            bool blocked = BlockedByClientFilters(request, transaction);
            if (blocked)
            {
                continue;
            }

            openBankingTransactions.Add(transaction);
        }

        foreach (IGrouping<string, OpenBankingTransaction> accountGrouping in openBankingTransactions.GroupBy(x =>
                     x.Account!.DisplayName))
        {
            decimal totalIn = accountGrouping.Where(x => !decimal.IsNegative(x.Amount)).Sum(x => x.Amount);
            decimal totalOut = accountGrouping.Where(x => decimal.IsNegative(x.Amount)).Sum(x => x.Amount);
            int totalTransactions = accountGrouping.Count();


            SpentInAccountReportResponse rsp = new()
            {
                TotalIn = totalIn,
                TotalOut = totalOut,
                TotalTransactions = totalTransactions,
                AccountName = (accountGrouping.FirstOrDefault()?.Account!).DisplayName };

            foreach (IGrouping<int, OpenBankingTransaction> yearlyGrouping in accountGrouping.GroupBy(x =>
                         x.TransactionTime.Year))
            {
                SpentInAccountReportYearlyBreakdownResponse yearGrp = new()
                {
                    Year = yearlyGrouping.Key,
                    TotalIn = yearlyGrouping.Where(x => !decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                    TotalOut = yearlyGrouping.Where(x => decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                    TotalTransactions = yearlyGrouping.Count()
                };

                foreach (IGrouping<int, OpenBankingTransaction> monthlyGrouping in yearlyGrouping.GroupBy(x =>
                             x.TransactionTime.Month))
                {
                    string monthName = DateTimeFormatInfo.CurrentInfo.GetMonthName(monthlyGrouping.Key);
                    SpentInAccountReportMonthlyBreakdownResponse monthGrp = new()
                    {
                        Month = monthName,
                        TotalIn = monthlyGrouping.Where(x => !decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                        TotalOut = monthlyGrouping.Where(x => decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                        TotalTransactions = monthlyGrouping.Count()
                    };


                    IEnumerable<IGrouping<int, OpenBankingTransaction>> dayGrp =
                        monthlyGrouping.GroupBy(x => x.TransactionTime.Day);
                    foreach (IGrouping<int, OpenBankingTransaction> dayGrouping in dayGrp)
                    {
                        monthGrp.DailyBreakdown.Add(new SpentInAccountReportDailyBreakdownResponse
                        {
                            Day = dayGrouping.Key,
                            TotalIn = dayGrouping.Where(x => !decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                            TotalOut = dayGrouping.Where(x => decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
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


    private IQueryable<OpenBankingTransaction> GetQueryByBaseReportRequest(BaseReportRequest request,
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

    private static bool BlockedByClientFilters(BaseReportRequest request, OpenBankingTransaction transaction)
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
