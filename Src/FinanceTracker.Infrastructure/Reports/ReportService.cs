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
    public ReportService(ICurrentUser currentUser, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        ILogger<ReportService> logger) : base(currentUser, financeTrackerContextFactory, logger)
    {
    }

    public async IAsyncEnumerable<SpentInTimePeriodReport> GetSpentInTimePeriodReportAsync(
        FilterTransactions request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        (List<BankTransaction> openBankingTransactions, Dictionary<Guid, string> customTags) =
            await GetTransactionsForReportAsync(request, context, cancellationToken);

        decimal totalIn = openBankingTransactions.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount);
        decimal totalOut = openBankingTransactions.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount);
        int totalTransactions = openBankingTransactions.Count;


        if (totalTransactions == 0)
        {
            yield break;
        }

        SpentInTimePeriodReport rsp = new()
        {
            TotalIn = totalIn, TotalOut = totalOut, TotalTransactions = totalTransactions
        };

        foreach (IGrouping<int, BankTransaction> yearlyGrouping in openBankingTransactions.GroupBy(static x =>
                     x.OccurredAt.Year))
        {
            SpentInTimePeriodReportYearlyBreakdown yearGrp = new()
            {
                Year = yearlyGrouping.Key,
                TotalIn = yearlyGrouping.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                TotalOut = yearlyGrouping.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                TotalTransactions = yearlyGrouping.Count()
            };

            foreach (IGrouping<int, BankTransaction> monthlyGrouping in yearlyGrouping.GroupBy(static x =>
                         x.OccurredAt.Month))
            {
                string monthName = DateTimeFormatInfo.CurrentInfo.GetMonthName(monthlyGrouping.Key);
                SpentInTimePeriodReportMonthlyBreakdown monthGrp = new()
                {
                    Month = monthName,
                    TotalIn = monthlyGrouping.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                    TotalOut = monthlyGrouping.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                    TotalTransactions = monthlyGrouping.Count()
                };


                IEnumerable<IGrouping<int, BankTransaction>> dayGrp =
                    monthlyGrouping.GroupBy(static x => x.OccurredAt.Day);
                foreach (IGrouping<int, BankTransaction> dayGrouping in dayGrp)
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
            IEnumerable<BankTransaction> transactions =
                openBankingTransactions.Where(x => TransactionCriteria.GetTags(x, customTags).Contains(classification));

            IEnumerable<BankTransaction> bankingTransactions = transactions.ToList();
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

            foreach (IGrouping<int, BankTransaction> yearlyGrouping in bankingTransactions.GroupBy(static x =>
                         x.OccurredAt.Year))
            {
                SpentInCategoryReportYearlyBreakdown yearGrp = new()
                {
                    Year = yearlyGrouping.Key,
                    TotalIn = yearlyGrouping.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                    TotalOut = yearlyGrouping.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                    TotalTransactions = yearlyGrouping.Count()
                };

                foreach (IGrouping<int, BankTransaction> monthlyGrouping in yearlyGrouping.GroupBy(static x =>
                             x.OccurredAt.Month))
                {
                    string monthName = DateTimeFormatInfo.CurrentInfo.GetMonthName(monthlyGrouping.Key);
                    SpentInCategoryReportMonthlyBreakdown monthGrp = new()
                    {
                        Month = monthName,
                        TotalIn = monthlyGrouping.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                        TotalOut = monthlyGrouping.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                        TotalTransactions = monthlyGrouping.Count()
                    };


                    IEnumerable<IGrouping<int, BankTransaction>> dayGrp =
                        monthlyGrouping.GroupBy(static x => x.OccurredAt.Day);
                    foreach (IGrouping<int, BankTransaction> dayGrouping in dayGrp)
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
            }

            yield return rsp;
        }
    }

    public async IAsyncEnumerable<SpentInAccountReport> GetAccountBreakdownReportAsync(
        FilterTransactions request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        // An account's own money in and out, so moves between the user's accounts count here
        (List<BankTransaction> openBankingTransactions, Dictionary<Guid, string> customTags) =
            await GetTransactionsForReportAsync(request, context, cancellationToken, includeTransfers: true);

        Dictionary<Guid, string> accountNames = await context.BankAccounts.OwnedBy(UserId)
            .ToDictionaryAsync(static x => x.Id, static x => x.DisplayName, cancellationToken);

        // By id, display names aren't unique across providers
        foreach (IGrouping<Guid, BankTransaction> accountGrouping in openBankingTransactions.GroupBy(static x =>
                     x.AccountId))
        {
            decimal totalIn = accountGrouping.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount);
            decimal totalOut = accountGrouping.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount);
            int totalTransactions = accountGrouping.Count();


            SpentInAccountReport rsp = new()
            {
                TotalIn = totalIn,
                TotalOut = totalOut,
                TotalTransactions = totalTransactions,
                AccountId = accountGrouping.Key,
                AccountName = accountNames[accountGrouping.Key]
            };

            foreach (IGrouping<int, BankTransaction> yearlyGrouping in accountGrouping.GroupBy(static x =>
                         x.OccurredAt.Year))
            {
                SpentInAccountReportYearlyBreakdown yearGrp = new()
                {
                    Year = yearlyGrouping.Key,
                    TotalIn = yearlyGrouping.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                    TotalOut = yearlyGrouping.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                    TotalTransactions = yearlyGrouping.Count()
                };

                foreach (IGrouping<int, BankTransaction> monthlyGrouping in yearlyGrouping.GroupBy(static x =>
                             x.OccurredAt.Month))
                {
                    string monthName = DateTimeFormatInfo.CurrentInfo.GetMonthName(monthlyGrouping.Key);
                    SpentInAccountReportMonthlyBreakdown monthGrp = new()
                    {
                        Month = monthName,
                        TotalIn = monthlyGrouping.Where(static x => !decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                        TotalOut = monthlyGrouping.Where(static x => decimal.IsNegative(x.Amount)).Sum(static x => x.Amount),
                        TotalTransactions = monthlyGrouping.Count()
                    };


                    IEnumerable<IGrouping<int, BankTransaction>> dayGrp =
                        monthlyGrouping.GroupBy(static x => x.OccurredAt.Day);
                    foreach (IGrouping<int, BankTransaction> dayGrouping in dayGrp)
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
            }

            yield return rsp;
        }
    }


    private async Task<(List<BankTransaction> Transactions, Dictionary<Guid, string> CustomTags)>
        GetTransactionsForReportAsync(FilterTransactions request, FinanceTrackerContext context,
            CancellationToken cancellationToken, bool includeTransfers = false)
    {
        TransactionCriteria criteria = TransactionCriteria.From(request);
        Dictionary<Guid, string> customTags = await context.CustomClassifications.OwnedBy(UserId)
            .ToDictionaryAsync(static x => x.Id, static x => x.Tag, cancellationToken);

        List<BankTransaction> transactions = await criteria
            .ApplyServerFilters(context.BankTransactions.OwnedBy(UserId), context)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Category and timestamps are encrypted, so filter and sort after decryption
        return (transactions
            .Where(x => includeTransfers || x.Category != BankTransaction.TransferCategory)
            .Where(x => criteria.MatchesClientFilters(x, customTags))
            .OrderByDescending(static x => x.OccurredAt)
            .ToList(), customTags);
    }
}
