using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using FinanceTracker.Data;
using FinanceTracker.Data.Models;
using FinanceTracker.Models.Request.Reports;
using FinanceTracker.Models.Response.Reports.Account;
using FinanceTracker.Models.Response.Reports.Category;
using FinanceTracker.Models.Response.Reports.SpentInTimePeriod;
using FinanceTracker.Services.OpenBanking;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Services.Reports;

public class ReportService : ServiceBase, IReportService
{
    private readonly IOpenBankingService _openBankingService;
    
    public ReportService(ClaimsPrincipal user, FinanceTrackerContext financeTrackerContext, IOpenBankingService openBankingService) : base(user, financeTrackerContext)
    {
        _openBankingService = openBankingService;
    }

    public async IAsyncEnumerable<SpentInTimePeriodReportResponse> GetSpentInTimePeriodReportAsync(BaseReportRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await _openBankingService.PerformSyncAsync(request.SyncTypes, cancellationToken);

        var query = GetQueryByBaseReportRequest(request);

        var openBankingTransactions = new List<OpenBankingTransaction>();
        
        await foreach (var transaction in query.OrderByDescending(x => x.TransactionTime)
                           .ToAsyncEnumerable().WithCancellation(cancellationToken))
        {
            openBankingTransactions.Add(transaction);
        }
        
        var totalIn = openBankingTransactions.Where(x => !Decimal.IsNegative(x.Amount)).Sum(x => x.Amount);
        var totalOut = openBankingTransactions.Where(x => Decimal.IsNegative(x.Amount)).Sum(x => x.Amount);
        var totalTransactions = openBankingTransactions.Count;
        


        foreach (var yearlyGrouping in openBankingTransactions.GroupBy(x => x.TransactionTime.Year))
        {
            
            var rsp = new SpentInTimePeriodReportResponse
            {
                TotalIn = totalIn,
                TotalOut = totalOut,
                TotalTransactions = totalTransactions
            };
            
            var yearGrp = new SpentInTimePeriodReportYearlyBreakdownResponse()
            {
                Year = yearlyGrouping.Key,
                TotalIn = yearlyGrouping.Where(x => !Decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                TotalOut = yearlyGrouping.Where(x => Decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                TotalTransactions = yearlyGrouping.Count(),
            };

            foreach (var monthlyGrouping in yearlyGrouping.GroupBy(x => x.TransactionTime.Month))
            {
                var monthName = DateTimeFormatInfo.CurrentInfo.GetMonthName(monthlyGrouping.Key);
                var monthGrp = new SpentInTimePeriodReportMonthlyBreakdownResponse()
                {
                    Month = monthName,
                    TotalIn = monthlyGrouping.Where(x => !Decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                    TotalOut = monthlyGrouping.Where(x => Decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                    TotalTransactions = monthlyGrouping.Count(),
                };
                
                
                var dayGrp = monthlyGrouping.GroupBy(x => x.TransactionTime.Day);
                foreach (var dayGrouping in dayGrp)
                {
                    monthGrp.DailyBreakdown.Add(new SpentInTimePeriodReportDailyBreakdownResponse()
                    {
                        Day = dayGrouping.Key,
                        TotalIn = dayGrouping.Where(x => !Decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                        TotalOut = dayGrouping.Where(x => Decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                        TotalTransactions = dayGrouping.Count()
                    });
                }
                
                yearGrp.MonthlyBreakdown.Add(monthGrp);
            }
            
            rsp.YearlyBreakdown.Add(yearGrp);
            yield return rsp;
        }
    }

    public async IAsyncEnumerable<SpentInCategoryReportResponse> GetCategoryBreakdownReportAsync(BaseReportRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await _openBankingService.PerformSyncAsync(request.SyncTypes, cancellationToken);

        var query = GetQueryByBaseReportRequest(request);

        var openBankingTransactions = new List<OpenBankingTransaction>();
        
        await foreach (var transaction in query.OrderByDescending(x => x.TransactionTime)
                           .ToAsyncEnumerable().WithCancellation(cancellationToken))
        {
            openBankingTransactions.Add(transaction);
        }

        var distinctClassifications = openBankingTransactions
            .SelectMany(x => x.Classifications.Select(c => c.Classification)).Distinct();
        

        foreach (var classification in distinctClassifications)
        { 
            var transactions = openBankingTransactions.Where(x => x.Classifications.Any(c => c.Classification == classification));
            
            var totalIn = transactions.Where(x => !Decimal.IsNegative(x.Amount)).Sum(x => x.Amount);
            var totalOut = transactions.Where(x => Decimal.IsNegative(x.Amount)).Sum(x => x.Amount);
            var totalTransactions = transactions.Count();
        

            var rsp = new SpentInCategoryReportResponse()
            {
                TotalIn = totalIn,
                TotalOut = totalOut,
                TotalTransactions = totalTransactions,
                Category = classification,
            };

            foreach (var yearlyGrouping in transactions.GroupBy(x => x.TransactionTime.Year))
            {
                var yearGrp = new SpentInCategoryReportYearlyBreakdownResponse()
                {
                    Year = yearlyGrouping.Key,
                    TotalIn = yearlyGrouping.Where(x => !Decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                    TotalOut = yearlyGrouping.Where(x => Decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                    TotalTransactions = yearlyGrouping.Count(),
                };

                foreach (var monthlyGrouping in yearlyGrouping.GroupBy(x => x.TransactionTime.Month))
                {
                    var monthName = DateTimeFormatInfo.CurrentInfo.GetMonthName(monthlyGrouping.Key);
                    var monthGrp = new SpentInCategoryReportMonthlyBreakdownResponse()
                    {
                        Month = monthName,
                        TotalIn = monthlyGrouping.Where(x => !Decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                        TotalOut = monthlyGrouping.Where(x => Decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                        TotalTransactions = monthlyGrouping.Count(),
                    };
                
                
                    var dayGrp = monthlyGrouping.GroupBy(x => x.TransactionTime.Day);
                    foreach (var dayGrouping in dayGrp)
                    {
                        monthGrp.DailyBreakdown.Add(new SpentInCategoryReportDailyBreakdownResponse()
                        {
                            Day = dayGrouping.Key,
                            TotalIn = dayGrouping.Where(x => !Decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                            TotalOut = dayGrouping.Where(x => Decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
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

    public async IAsyncEnumerable<SpentInAccountReportResponse> GetAccountBreakdownReportAsync(BaseReportRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await _openBankingService.PerformSyncAsync(request.SyncTypes, cancellationToken);

        var query = GetQueryByBaseReportRequest(request);

        var openBankingTransactions = new List<OpenBankingTransaction>();
        
        await foreach (var transaction in query.OrderByDescending(x => x.TransactionTime)
                           .ToAsyncEnumerable().WithCancellation(cancellationToken))
        {
            openBankingTransactions.Add(transaction);
        }

        foreach (var accountGrouping in openBankingTransactions.GroupBy(x => x.Account.DisplayName))
        { 
            var totalIn = accountGrouping.Where(x => !Decimal.IsNegative(x.Amount)).Sum(x => x.Amount);
            var totalOut = accountGrouping.Where(x => Decimal.IsNegative(x.Amount)).Sum(x => x.Amount);
            var totalTransactions = accountGrouping.Count();
        

            var rsp = new SpentInAccountReportResponse()
            {
                TotalIn = totalIn,
                TotalOut = totalOut,
                TotalTransactions = totalTransactions,
                AccountName = accountGrouping.FirstOrDefault().Account.DisplayName ?? "",
            };

            foreach (var yearlyGrouping in accountGrouping.GroupBy(x => x.TransactionTime.Year))
            {
                var yearGrp = new SpentInAccountReportYearlyBreakdownResponse()
                {
                    Year = yearlyGrouping.Key,
                    TotalIn = yearlyGrouping.Where(x => !Decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                    TotalOut = yearlyGrouping.Where(x => Decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                    TotalTransactions = yearlyGrouping.Count(),
                };

                foreach (var monthlyGrouping in yearlyGrouping.GroupBy(x => x.TransactionTime.Month))
                {
                    var monthName = DateTimeFormatInfo.CurrentInfo.GetMonthName(monthlyGrouping.Key);
                    var monthGrp = new SpentInAccountReportMonthlyBreakdownResponse()
                    {
                        Month = monthName,
                        TotalIn = monthlyGrouping.Where(x => !Decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                        TotalOut = monthlyGrouping.Where(x => Decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                        TotalTransactions = monthlyGrouping.Count(),
                    };
                
                
                    var dayGrp = monthlyGrouping.GroupBy(x => x.TransactionTime.Day);
                    foreach (var dayGrouping in dayGrp)
                    {
                        monthGrp.DailyBreakdown.Add(new SpentInAccountReportDailyBreakdownResponse()
                        {
                            Day = dayGrouping.Key,
                            TotalIn = dayGrouping.Where(x => !Decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
                            TotalOut = dayGrouping.Where(x => Decimal.IsNegative(x.Amount)).Sum(x => x.Amount),
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
    
    private IQueryable<OpenBankingTransaction> GetQueryByBaseReportRequest(BaseReportRequest request)
    {
        var query = _financeTrackerContext.IsolateToUser(UserId)
            .Include(x => x.Providers)
            .ThenInclude(x => x.Accounts)
            .ThenInclude(x => x.Transactions)
            .SelectMany(x => x.Providers.SelectMany(c => c.Transactions))
            .Include(x => x.Account)
            .Include(x => x.Classifications)
            .AsNoTracking();


        if (request.AccountIds is not null && request.AccountIds.Any())
        {
            query = query.Where(x => request.AccountIds.Contains(x.Account.Id));
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
            query = query.Where(x => request.Tags.Any(y => x.Classifications.Any(c => c.Classification == y)));
        }

        if (request.ProviderIds is not null && request.ProviderIds.Any())
        {
                
            query = query.Where(x => request.ProviderIds.Contains(x.Provider.Id))
                .Select(x => x);
        }

        if (request.SearchTerm is not null)
        {
            query = query.Where(x => EF.Functions.Like(x.Description.ToLower(), $"%{request.SearchTerm.ToLower()}%"));

        }

        if (request.FromDate is not null)
        {
            query = query.Where(x => x.TransactionTime >= request.FromDate);
        }

        if (request.ToDate is not null)
        {
            query = query.Where(x => x.TransactionTime <= request.ToDate);
        }
            
        query = query.Where(x => x.TransactionCategory != "TRANSFER");
            
        
        return query;
    }
}