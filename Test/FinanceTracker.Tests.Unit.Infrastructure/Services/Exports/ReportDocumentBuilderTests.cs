using System.Globalization;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Exports;
using FinanceTracker.Contracts.Reports;
using FinanceTracker.Contracts.Reports.Account;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.Reports.SpentInTimePeriod;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Infrastructure.Exports.Reports;
using FinanceTracker.Tests.Unit.Shared;
using NSubstitute;
using TUnit.Assertions.Enums;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Exports;

public class ReportDocumentBuilderTests : UnitTestBase
{
    private readonly IReportService _reportService = Substitute.For<IReportService>();
    private readonly ITransactionsService _transactionsService = Substitute.For<ITransactionsService>();

    public ReportDocumentBuilderTests()
    {
        _reportService.GetSpentInTimePeriodReportAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<SpentInTimePeriodReport>());
        _reportService.GetAccountBreakdownReportAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<SpentInAccountReport>());
        _reportService.GetCategoryBreakdownReportAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<SpentInCategoryReport>());
    }

    [Test]
    public async Task BuildAsync_OverTime_HasEveryYearOldestFirstWithAllTwelveMonths()
    {
        // Arrange: in the app's culture, the month's short name differing between ICU versions, e.g. Sep or Sept
        using CultureScope culture = new();
        string septemberThe30th = new DateOnly(2026, 9, 30).ToString("d MMM", CultureInfo.CurrentCulture);
        _reportService.GetSpentInTimePeriodReportAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new SpentInTimePeriodReport
                {
                    TotalIn = 300m, TotalOut = -120m, TotalTransactions = 3,
                    YearlyBreakdown =
                    [
                        Year(2026, Month("October", new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 29), 200m)),
                        Year(2025, Month("December", new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 31), 100m))
                    ]
                }
            }.ToAsyncEnumerable());

        // Act
        ReportContent report = await BuildAsync(ReportBreakdown.OverTime);

        // Assert
        ReportRow everything = report.Subjects.Single();
        ReportRow october = everything.Periods[1].Periods[9];
        using (Assert.Multiple())
        {
            await Assert.That(everything.Title).IsEqualTo("All accounts");
            await Assert.That(everything.Periods.Select(static x => x.Title))
                .IsEquivalentTo(["2025", "2026"], CollectionOrdering.Matching);
            await Assert.That(everything.Periods[1].Periods.Count).IsEqualTo(12);
            await Assert.That(october.Title).IsEqualTo("October 2026");
            await Assert.That(october.Label).IsEqualTo("Oct");
            await Assert.That(october.TotalIn).IsEqualTo(200m);
            await Assert.That(october.Dates).IsEqualTo($"{septemberThe30th} to 29 Oct 2026");
            await Assert.That(everything.Periods[0].Periods[11].Dates).IsNull();
            await Assert.That(everything.Periods[1].Periods[0].TotalTransactions).IsEqualTo(0);
        }
    }

    [Test]
    public async Task BuildAsync_ByTag_MostMoneyOutFirstLeavingOutThoseWithNothing()
    {
        // Arrange
        _reportService.GetCategoryBreakdownReportAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new SpentInCategoryReport { Category = "Groceries", TotalOut = -120m, TotalTransactions = 4 },
                new SpentInCategoryReport { Category = "Unused" },
                new SpentInCategoryReport { Category = "Rent", TotalOut = -850m, TotalTransactions = 1 }
            }.ToAsyncEnumerable());

        // Act
        ReportContent report = await BuildAsync(ReportBreakdown.ByTag);

        // Assert
        await Assert.That(report.Subjects.Select(static x => x.Title))
            .IsEquivalentTo(["Rent", "Groceries"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task BuildAsync_ByAccount_IsEachAccount()
    {
        // Arrange
        _reportService.GetAccountBreakdownReportAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new SpentInAccountReport { AccountName = "Main", TotalOut = -10m, TotalTransactions = 1 }
            }.ToAsyncEnumerable());

        // Act
        ReportContent report = await BuildAsync(ReportBreakdown.ByAccount);

        // Assert
        await Assert.That(report.Subjects.Single().Title).IsEqualTo("Main");
    }

    [Test]
    public async Task BuildAsync_NamesTheFiltersAsTheFilterBarDoes()
    {
        // Arrange
        Guid main = Guid.NewGuid();
        _transactionsService.GetAccountsForTransactionFiltersAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new TransactionAccountFilter { AccountId = main, AccountName = "Main" },
            new TransactionAccountFilter { AccountId = Guid.NewGuid(), AccountName = "Savings" }
        }.ToAsyncEnumerable());
        FilterTransactions filters = new()
        {
            AccountIds = [main],
            Tags = ["Groceries", "Household"],
            SearchTerm = " shop ",
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 6, 30)
        };

        // Act
        ReportContent report = await BuildAsync(ReportBreakdown.OverTime, filters);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Filters).IsEquivalentTo(
                ["Accounts: Main", "Tags: Groceries, Household", "Description contains “shop”"],
                CollectionOrdering.Matching);
            await Assert.That(report.From).IsEqualTo(new DateOnly(2026, 1, 1));
            await Assert.That(report.To).IsEqualTo(new DateOnly(2026, 6, 30));
        }
    }

    [Test]
    public async Task BuildAsync_NoFilters_DescribesNone()
    {
        // Act
        ReportContent report = await BuildAsync(ReportBreakdown.OverTime);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Filters).IsEmpty();
            await Assert.That(report.From).IsNull();
            await Assert.That(report.To).IsNull();
        }
    }

    private Task<ReportContent> BuildAsync(ReportBreakdown breakdown, FilterTransactions? filters = null) =>
        BuildBuilder(_reportService, _transactionsService).BuildAsync(
            new ExportReport { Breakdown = breakdown, Filters = filters ?? new FilterTransactions() },
            CancellationToken);

    private static SpentInTimePeriodReportYearlyBreakdown Year(int year,
        SpentInTimePeriodReportMonthlyBreakdown month) => new()
    {
        Year = year,
        TotalIn = month.TotalIn,
        TotalTransactions = month.TotalTransactions,
        MonthlyBreakdown = [month]
    };

    private static SpentInTimePeriodReportMonthlyBreakdown Month(string name, DateOnly start, DateOnly end,
        decimal moneyIn) => new()
    {
        Month = name, Start = start, End = end, TotalIn = moneyIn, TotalTransactions = 1
    };

    /// <summary>
    /// A <see cref="ReportDocumentBuilder"/> whose services have nothing, apart from those given.
    /// </summary>
    internal static ReportDocumentBuilder BuildBuilder(IReportService? reportService = null,
        ITransactionsService? transactionsService = null)
    {
        IBillsService billsService = Substitute.For<IBillsService>();
        billsService.GetBillsAsync(Arg.Any<CancellationToken>()).Returns(_ => AsyncEnumerable.Empty<Bill>());
        IDebtsService debtsService = Substitute.For<IDebtsService>();
        debtsService.GetDebtsAsync(Arg.Any<CancellationToken>()).Returns(_ => AsyncEnumerable.Empty<Debt>());
        ISavingsGoalsService savingsGoalsService = Substitute.For<ISavingsGoalsService>();
        savingsGoalsService.GetSavingsGoalsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<SavingsGoal>());

        return new ReportDocumentBuilder(reportService ?? Substitute.For<IReportService>(),
            transactionsService ?? Substitute.For<ITransactionsService>(), billsService, debtsService,
            savingsGoalsService, new FixedTimeProvider(new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc)));
    }
}
