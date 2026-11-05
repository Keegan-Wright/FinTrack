using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Infrastructure.Exports.Statements;
using FinanceTracker.Tests.Unit.Shared;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Exports;

public class StatementBuilderTests : UnitTestBase
{
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    private readonly IBudgetsService _budgetsService = Substitute.For<IBudgetsService>();
    private readonly IDashboardService _dashboardService = Substitute.For<IDashboardService>();
    private readonly IReportService _reportService = Substitute.For<IReportService>();
    private readonly ITransactionsService _transactionsService = Substitute.For<ITransactionsService>();

    public StatementBuilderTests()
    {
        // November's pay period, from pay on 30 October, in British Summer Time until 25 October only
        _budgetsService.GetMonthAsync(2026, 11, Arg.Any<CancellationToken>()).Returns(new BudgetMonthOverview
        {
            Month = new DateOnly(2026, 11, 1), Start = new DateOnly(2026, 10, 30), End = new DateOnly(2026, 11, 27)
        });
        _dashboardService.GetSpentInTimePeriod(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new SpentInTimePeriod());
        _reportService.GetCategoryBreakdownReportAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<SpentInCategoryReport>());
        _transactionsService.GetAllTransactionsAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<Transaction>());
    }

    [Test]
    public async Task BuildAsync_IsThePayPeriodTheBudgetsPageUses()
    {
        // Act
        Statement statement = await BuildAsync();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(statement.Month).IsEqualTo(new DateOnly(2026, 11, 1));
            await Assert.That(statement.Start).IsEqualTo(new DateOnly(2026, 10, 30));
            await Assert.That(statement.End).IsEqualTo(new DateOnly(2026, 11, 27));
            await Assert.That(statement.FromPayDay).IsTrue();
        }
    }

    [Test]
    public async Task BuildAsync_ReadsFromTheFirstDayToTheEndOfTheLastWhereTheUserIs()
    {
        // Arrange: in Phoenix, which has no daylight saving, a day starts at 07:00 UTC
        TimeZoneInfo phoenix = TimeZoneInfo.FindSystemTimeZoneById("America/Phoenix");
        DateTime from = new(2026, 10, 30, 7, 0, 0, DateTimeKind.Utc);
        DateTime until = new(2026, 11, 28, 7, 0, 0, DateTimeKind.Utc);

        // Act
        await BuildAsync(phoenix);

        // Assert
        using (Assert.Multiple())
        {
            await _dashboardService.Received(1).GetSpentInTimePeriod(from, until, Arg.Any<CancellationToken>());
            _transactionsService.Received(1).GetAllTransactionsAsync(
                Arg.Is<FilterTransactions>(x => x.FromDate == from && x.ToDate == until.AddTicks(-1)),
                Arg.Any<CancellationToken>());
            _reportService.Received(1).GetCategoryBreakdownReportAsync(
                Arg.Is<FilterTransactions>(x => x.FromDate == from && x.ToDate == until.AddTicks(-1)),
                Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task BuildAsync_TagsAreOnlyThoseSpentOnMostFirst()
    {
        // Arrange
        _reportService.GetCategoryBreakdownReportAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new SpentInCategoryReport { Category = "Groceries", TotalOut = -120m },
                new SpentInCategoryReport { Category = "Salary", TotalIn = 2000m },
                new SpentInCategoryReport { Category = "Rent", TotalOut = -850m }
            }.ToAsyncEnumerable());

        // Act
        Statement statement = await BuildAsync();

        // Assert
        await Assert.That(statement.Tags.Select(static x => x.Category)).IsEquivalentTo(["Rent", "Groceries"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task BuildAsync_TransactionsAreOldestFirst()
    {
        // Arrange
        _transactionsService.GetAllTransactionsAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                TransactionCsvTests.Transaction("NEWER", -2m, at: new DateTime(2026, 11, 20, 0, 0, 0)),
                TransactionCsvTests.Transaction("OLDER", -1m, at: new DateTime(2026, 11, 2, 0, 0, 0))
            }.ToAsyncEnumerable());

        // Act
        Statement statement = await BuildAsync();

        // Assert
        await Assert.That(statement.Transactions.Select(static x => x.Description)).IsEquivalentTo(
            ["OLDER", "NEWER"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private Task<Statement> BuildAsync(TimeZoneInfo? timeZone = null) =>
        BuildBuilder(_budgetsService, _dashboardService, _reportService, _transactionsService, timeZone ?? London)
            .BuildAsync(2026, 11, CancellationToken);

    /// <summary>
    /// A <see cref="StatementBuilder"/> whose services have nothing, apart from those given.
    /// </summary>
    internal static StatementBuilder BuildBuilder(IBudgetsService? budgetsService = null,
        IDashboardService? dashboardService = null, IReportService? reportService = null,
        ITransactionsService? transactionsService = null, TimeZoneInfo? timeZone = null)
    {
        IBillsService billsService = Substitute.For<IBillsService>();
        billsService.GetMonthAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new BillMonthOverview());
        ISavingsGoalsService savingsGoalsService = Substitute.For<ISavingsGoalsService>();
        savingsGoalsService.GetSavingsGoalsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<SavingsGoal>());
        IDebtsService debtsService = Substitute.For<IDebtsService>();
        debtsService.GetDebtsAsync(Arg.Any<CancellationToken>()).Returns(_ => AsyncEnumerable.Empty<Debt>());

        return new StatementBuilder(budgetsService ?? Substitute.For<IBudgetsService>(), billsService,
            dashboardService ?? Substitute.For<IDashboardService>(), reportService ?? Substitute.For<IReportService>(),
            transactionsService ?? Substitute.For<ITransactionsService>(), savingsGoalsService, debtsService,
            new FixedTimeProvider(new DateTime(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc), timeZone));
    }
}
