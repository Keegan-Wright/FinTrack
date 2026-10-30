using System.Globalization;
using FinanceTracker.Contracts.Reports;
using FinanceTracker.Contracts.Reports.Account;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.Reports.SpentInTimePeriod;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Reports;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using FinanceTracker.Domain.OpenBanking.Transfers;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Reports;

public class ReportServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    private static readonly DateTime January15 = new(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime January20 = new(2025, 1, 20, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime February3 = new(2025, 2, 3, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime March1NextYear = new(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task GetSpentInTimePeriodReportAsync_ReturnsTotals()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("income", "main", Transaction(200m, January15)))
            .Do(s => s.TransactionScenario.WithTransaction("expense", "main", Transaction(-50m, January15)));

        ReportService subject = BuildSubject();

        // Act
        SpentInTimePeriodReport report = (await subject.GetSpentInTimePeriodReportAsync(Request(), CancellationToken)
            .ToListAsync(CancellationToken)).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.TotalIn).IsEqualTo(200m);
            await Assert.That(report.TotalOut).IsEqualTo(-50m);
            await Assert.That(report.TotalTransactions).IsEqualTo(2);
        }
    }

    [Test]
    public async Task GetSpentInTimePeriodReportAsync_LeavesOutTransfers()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("income", "main", Transaction(200m, January15)))
            .Do(s => s.TransactionScenario.WithTransaction("to-savings", "main", Transaction(-1000m, January15)))
            .Do(s => s.TransactionScenario.WithTransfer("to-savings"));

        ReportService subject = BuildSubject();

        // Act
        SpentInTimePeriodReport report = (await subject.GetSpentInTimePeriodReportAsync(Request(), CancellationToken)
            .ToListAsync(CancellationToken)).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.TotalIn).IsEqualTo(200m);
            await Assert.That(report.TotalOut).IsEqualTo(0m);
            await Assert.That(report.TotalTransactions).IsEqualTo(1);
        }
    }

    [Test]
    public async Task GetCategoryBreakdownReportAsync_LeavesOutTransfers()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("shopping", "main",
                Transaction(-30m, January15, classifications: ["Savings"])))
            .Do(s => s.TransactionScenario.WithTransaction("to-savings", "main",
                Transaction(-1000m, January15, classifications: ["Savings"])))
            .Do(s => s.TransactionScenario.WithTransfer("to-savings"));

        ReportService subject = BuildSubject();

        // Act
        SpentInCategoryReport report = (await subject.GetCategoryBreakdownReportAsync(Request(), CancellationToken)
            .ToListAsync(CancellationToken)).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Category).IsEqualTo("Savings");
            await Assert.That(report.TotalOut).IsEqualTo(-30m);
            await Assert.That(report.TotalTransactions).IsEqualTo(1);
        }
    }

    [Test]
    public async Task GetAccountBreakdownReportAsync_KeepsTransfers()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("shopping", "main", Transaction(-30m, January15)))
            .Do(s => s.TransactionScenario.WithTransaction("from-current", "main", Transaction(250m, January20)))
            .Do(s => s.TransactionScenario.WithTransfer("from-current"));

        ReportService subject = BuildSubject();

        // Act
        SpentInAccountReport report = (await subject.GetAccountBreakdownReportAsync(Request(), CancellationToken)
            .ToListAsync(CancellationToken)).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.TotalIn).IsEqualTo(250m);
            await Assert.That(report.TotalOut).IsEqualTo(-30m);
            await Assert.That(report.TotalTransactions).IsEqualTo(2);
        }
    }

    [Test]
    public async Task GetSpentInTimePeriodReportAsync_BreaksDownByYearMonthAndDay()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("jan-15-a", "main", Transaction(-10m, January15)))
            .Do(s => s.TransactionScenario.WithTransaction("jan-15-b", "main", Transaction(30m, January15)))
            .Do(s => s.TransactionScenario.WithTransaction("jan-20", "main", Transaction(-20m, January20)))
            .Do(s => s.TransactionScenario.WithTransaction("feb-3", "main", Transaction(-5m, February3)))
            .Do(s => s.TransactionScenario.WithTransaction("next-year", "main", Transaction(-1m, March1NextYear)));

        ReportService subject = BuildSubject();

        // Act
        SpentInTimePeriodReport report = (await subject.GetSpentInTimePeriodReportAsync(Request(), CancellationToken)
            .ToListAsync(CancellationToken)).Single();

        // Assert
        SpentInTimePeriodReportYearlyBreakdown year2025 = report.YearlyBreakdown.Single(x => x.Year == 2025);
        SpentInTimePeriodReportMonthlyBreakdown january = year2025.MonthlyBreakdown.Single(x => x.Month == MonthName(1));
        SpentInTimePeriodReportDailyBreakdown january15 = january.DailyBreakdown.Single(x => x.Day == 15);

        using (Assert.Multiple())
        {
            await Assert.That(report.YearlyBreakdown.Select(x => x.Year)).IsEquivalentTo([2025, 2026]);
            await Assert.That(year2025.TotalTransactions).IsEqualTo(4);
            await Assert.That(year2025.MonthlyBreakdown.Count).IsEqualTo(2);
            await Assert.That(january.TotalIn).IsEqualTo(30m);
            await Assert.That(january.TotalOut).IsEqualTo(-30m);
            await Assert.That(january.DailyBreakdown.Count).IsEqualTo(2);
            await Assert.That(january15.TotalTransactions).IsEqualTo(2);
            await Assert.That(january15.TotalIn).IsEqualTo(30m);
            await Assert.That(january15.TotalOut).IsEqualTo(-10m);
        }
    }

    [Test]
    public async Task GetSpentInTimePeriodReportAsync_AppliesDateAndAccountFilters()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.BankAccountScenario.WithAccount("savings", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("in-range", "main", Transaction(-10m, January20)))
            .Do(s => s.TransactionScenario.WithTransaction("too-early", "main", Transaction(-20m, January15)))
            .Do(s => s.TransactionScenario.WithTransaction("too-late", "main", Transaction(-40m, February3)))
            .Do(s => s.TransactionScenario.WithTransaction("other-account", "savings", Transaction(-80m, January20)));

        ReportService subject = BuildSubject();
        FilterTransactions request = new()
        {
            AccountIds = [Scenario.AccountMappings["main"]],
            FromDate = January15.AddDays(1),
            ToDate = February3.AddDays(-1)
        };

        // Act
        SpentInTimePeriodReport report = (await subject.GetSpentInTimePeriodReportAsync(request, CancellationToken)
            .ToListAsync(CancellationToken)).Single();

        // Assert
        await Assert.That(report.TotalOut).IsEqualTo(-10m);
    }

    [Test]
    public async Task GetSpentInTimePeriodReportAsync_OnlyIncludesCurrentUsersTransactions()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("other-bank", OtherUsername))
            .Do(s => s.BankAccountScenario.WithAccount("other", "other-bank"))
            .Do(s => s.TransactionScenario.WithTransactions("other", 3));

        ReportService subject = BuildSubject();

        // Act
        List<SpentInTimePeriodReport> reports = await subject.GetSpentInTimePeriodReportAsync(Request(), CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(reports.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetSpentInTimePeriodReportAsync_WhenUserHasNoTransactions_ReturnsEmpty()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        ReportService subject = BuildSubject();

        // Act
        List<SpentInTimePeriodReport> reports = await subject.GetSpentInTimePeriodReportAsync(Request(), CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(reports.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetCategoryBreakdownReportAsync_ReturnsOneReportPerProviderAndCustomTag()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.ClassificationScenario.WithClassification("holiday", Username, "Holiday"))
            .Do(s => s.TransactionScenario.WithTransaction("food", "main", Transaction(-10m, January15, classifications: ["Food"])))
            .Do(s => s.TransactionScenario.WithTransaction("food-travel", "main",
                Transaction(-20m, January20, classifications: ["Food", "Travel"])))
            .Do(s => s.TransactionScenario.WithTransaction("refund", "main", Transaction(5m, February3, classifications: ["Food"])))
            .Do(s => s.TransactionScenario.WithTransaction("untagged", "main", Transaction(-99m, February3)))
            .Do(s => s.TransactionScenario.WithCustomClassification("food-travel", "holiday"));

        ReportService subject = BuildSubject();

        // Act
        List<SpentInCategoryReport> reports = await subject.GetCategoryBreakdownReportAsync(Request(), CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        SpentInCategoryReport food = reports.Single(x => x.Category == "Food");
        SpentInCategoryReport holiday = reports.Single(x => x.Category == "Holiday");

        using (Assert.Multiple())
        {
            await Assert.That(reports.Select(x => x.Category)).IsEquivalentTo(["Food", "Travel", "Holiday"]);
            await Assert.That(food.TotalTransactions).IsEqualTo(3);
            await Assert.That(food.TotalIn).IsEqualTo(5m);
            await Assert.That(food.TotalOut).IsEqualTo(-30m);
            await Assert.That(food.YearlyBreakdown.Single().MonthlyBreakdown.Count).IsEqualTo(2);
            await Assert.That(holiday.TotalTransactions).IsEqualTo(1);
            await Assert.That(holiday.TotalOut).IsEqualTo(-20m);
        }
    }

    [Test]
    public async Task GetCategoryBreakdownReportAsync_IncludesDailyBreakdown()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("food-a", "main", Transaction(-10m, January15, classifications: ["Food"])))
            .Do(s => s.TransactionScenario.WithTransaction("food-b", "main", Transaction(4m, January15, classifications: ["Food"])))
            .Do(s => s.TransactionScenario.WithTransaction("food-c", "main", Transaction(-2m, January20, classifications: ["Food"])));

        ReportService subject = BuildSubject();

        // Act
        SpentInCategoryReport report = (await subject.GetCategoryBreakdownReportAsync(Request(), CancellationToken)
            .ToListAsync(CancellationToken)).Single();

        // Assert
        IList<SpentInCategoryReportDailyBreakdown> days = report.YearlyBreakdown.Single().MonthlyBreakdown.Single().DailyBreakdown;
        SpentInCategoryReportDailyBreakdown january15 = days.Single(x => x.Day == 15);

        using (Assert.Multiple())
        {
            await Assert.That(days.Select(x => x.Day)).IsEquivalentTo([15, 20]);
            await Assert.That(january15.TotalTransactions).IsEqualTo(2);
            await Assert.That(january15.TotalIn).IsEqualTo(4m);
            await Assert.That(january15.TotalOut).IsEqualTo(-10m);
        }
    }

    [Test]
    public async Task GetCategoryBreakdownReportAsync_AppliesTagFilter()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("food", "main", Transaction(-10m, January15, classifications: ["Food"])))
            .Do(s => s.TransactionScenario.WithTransaction("travel", "main", Transaction(-20m, January15, classifications: ["Travel"])));

        ReportService subject = BuildSubject();
        FilterTransactions request = new() { Tags = ["Travel"] };

        // Act
        List<SpentInCategoryReport> reports = await subject.GetCategoryBreakdownReportAsync(request, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(reports.Select(x => x.Category)).IsEquivalentTo(["Travel"]);
    }

    [Test]
    public async Task GetCategoryBreakdownReportAsync_HiddenBankClassification_LeftOutOfThatTag()
    {
        // Arrange, the bank calls the mortgage TV as well as the licence
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("mortgage", "main",
                Transaction(-950m, January15, classifications: ["Bills", "TV"])))
            .Do(s => s.TransactionScenario.WithTransaction("licence", "main",
                Transaction(-15m, January20, classifications: ["TV"])));
        await HideAsync("mortgage", "TV");

        ReportService subject = BuildSubject();

        // Act
        List<SpentInCategoryReport> reports = await subject.GetCategoryBreakdownReportAsync(Request(), CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        SpentInCategoryReport tv = reports.Single(x => x.Category == "TV");
        SpentInCategoryReport bills = reports.Single(x => x.Category == "Bills");

        using (Assert.Multiple())
        {
            await Assert.That(tv.TotalTransactions).IsEqualTo(1);
            await Assert.That(tv.TotalOut).IsEqualTo(-15m);
            await Assert.That(bills.TotalOut).IsEqualTo(-950m);
        }
    }

    [Test]
    public async Task GetCategoryBreakdownReportAsync_ClassificationHiddenEverywhere_HasNoReport()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("mortgage", "main",
                Transaction(-950m, January15, classifications: ["Bills", "TV"])));
        await HideAsync("mortgage", "TV");

        ReportService subject = BuildSubject();

        // Act
        List<SpentInCategoryReport> reports = await subject.GetCategoryBreakdownReportAsync(Request(), CancellationToken)
            .ToListAsync(CancellationToken);
        List<SpentInCategoryReport> filteredToTv = await subject
            .GetCategoryBreakdownReportAsync(new FilterTransactions { Tags = ["TV"] }, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(reports.Select(x => x.Category)).IsEquivalentTo(["Bills"]);
            await Assert.That(filteredToTv).IsEmpty();
        }
    }

    [Test]
    public async Task GetAccountBreakdownReportAsync_ReturnsOneReportPerAccount()
    {
        // Arrange
        await SeedAccountAsync(displayName: "Current")
            .Do(s => s.BankConnectionScenario.WithConnection("other-bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("other-current", "other-bank", (userId, connectionId) =>
                new BankAccount(userId, connectionId, "acc-other", "TRANSACTION", "Current", "GBP")))
            .Do(s => s.TransactionScenario.WithTransaction("main-1", "main", Transaction(-10m, January15)))
            .Do(s => s.TransactionScenario.WithTransaction("main-2", "main", Transaction(100m, February3)))
            .Do(s => s.TransactionScenario.WithTransaction("other-1", "other-current", Transaction(-5m, January20)));

        ReportService subject = BuildSubject();

        // Act
        List<SpentInAccountReport> reports = await subject.GetAccountBreakdownReportAsync(Request(), CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        SpentInAccountReport main = reports.Single(x => x.TotalTransactions == 2);
        SpentInAccountReport other = reports.Single(x => x.TotalTransactions == 1);

        using (Assert.Multiple())
        {
            // Accounts with the same display name are still reported separately
            await Assert.That(reports.Count).IsEqualTo(2);
            await Assert.That(main.AccountName).IsEqualTo("Current");
            await Assert.That(main.TotalIn).IsEqualTo(100m);
            await Assert.That(main.TotalOut).IsEqualTo(-10m);
            await Assert.That(main.YearlyBreakdown.Single().MonthlyBreakdown.Count).IsEqualTo(2);
            await Assert.That(main.YearlyBreakdown.Single().MonthlyBreakdown.Sum(x => x.DailyBreakdown.Count)).IsEqualTo(2);
            await Assert.That(other.AccountName).IsEqualTo("Current");
            await Assert.That(other.TotalOut).IsEqualTo(-5m);
        }
    }

    [Test]
    public async Task GetAccountBreakdownReportAsync_WhenUserHasNoTransactions_ReturnsEmpty()
    {
        // Arrange
        await SeedAccountAsync();
        ReportService subject = BuildSubject();

        // Act
        List<SpentInAccountReport> reports = await subject.GetAccountBreakdownReportAsync(Request(), CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(reports.Count).IsEqualTo(0);
    }

    private Task<TestingScenario> SeedAccountAsync(string? displayName = null) =>
        Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank", displayName is null
                ? null
                : (userId, connectionId) => new BankAccount(userId, connectionId, "acc-main", "TRANSACTION", displayName, "GBP")));

    private static TransactionDetails Transaction(decimal amount, DateTime occurredAt, string category = "PURCHASE",
        string[]? classifications = null) =>
        new TransactionDetailsFaker().Generate() with
        {
            Amount = amount,
            OccurredAt = occurredAt,
            Category = category,
            ProviderClassifications = classifications ?? []
        };

    private static FilterTransactions Request() => new();

    private static string MonthName(int month) => DateTimeFormatInfo.CurrentInfo.GetMonthName(month);

    private async Task HideAsync(string transactionName, string classification)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid id = Scenario.TransactionMappings[transactionName];
        BankTransaction transaction = await context.BankTransactions.SingleAsync(x => x.Id == id, CancellationToken);
        transaction.HideClassification(classification);
        await context.SaveChangesAsync(CancellationToken);
    }

    private ReportService BuildSubject() => new(CurrentUserFor(Username), DbContextFactory, NullLogger<ReportService>.Instance);
}
