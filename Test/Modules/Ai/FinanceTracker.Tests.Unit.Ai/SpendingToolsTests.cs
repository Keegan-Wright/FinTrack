using System.Text.Json;
using FinanceTracker.Contracts.Reports.Account;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.Reports.SpentInTimePeriod;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using TUnit.Assertions.Enums;

namespace FinanceTracker.Tests.Unit.Ai;

public class SpendingToolsTests : AiToolTestBase
{
    private readonly IReportService _reportService = Substitute.For<IReportService>();

    public SpendingToolsTests()
    {
        Services.AddSingleton(_reportService);
    }

    [Test]
    public async Task GetTotals_GivesMoneyOutAsAPositiveAmount()
    {
        // Arrange
        _reportService.GetSpentInTimePeriodReportAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new SpentInTimePeriodReport { TotalIn = 2000m, TotalOut = -750.5m, TotalTransactions = 12 } }
                .ToAsyncEnumerable());

        // Act
        JsonElement totals = await CallAsync("get_totals", new { from = "2026-05-01", to = "2026-05-31" });

        // Assert
        await Assert.That(totals.GetProperty("moneyIn").GetDecimal()).IsEqualTo(2000m);
        await Assert.That(totals.GetProperty("moneyOut").GetDecimal()).IsEqualTo(750.5m);
        await Assert.That(totals.GetProperty("net").GetDecimal()).IsEqualTo(1249.5m);
        await Assert.That(totals.GetProperty("transactions").GetInt32()).IsEqualTo(12);
        await Assert.That(totals.GetProperty("link").GetString()).IsEqualTo("/Reports");
    }

    [Test]
    public async Task GetTotals_FiltersWholeDaysAndTheGivenAccountsAndTags()
    {
        // Arrange
        Guid account = Guid.NewGuid();
        FilterTransactions? filter = null;
        _reportService.GetSpentInTimePeriodReportAsync(Arg.Do<FilterTransactions>(x => filter = x),
                Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<SpentInTimePeriodReport>());

        // Act
        await CallAsync("get_totals",
            new { from = "2026-05-01", to = "2026-05-31", accountIds = new[] { account }, tags = new[] { "Takeaways" } });

        // Assert
        await Assert.That(filter!.FromDate).IsEqualTo(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        await Assert.That(filter.ToDate).IsEqualTo(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1));
        await Assert.That(filter.AccountIds!).IsEquivalentTo([account]);
        await Assert.That(filter.Tags!).IsEquivalentTo(["Takeaways"]);
    }

    [Test]
    public async Task GetSpendingBreakdown_ByTag_PutsTheBiggestSpendingFirst()
    {
        // Arrange
        _reportService.GetCategoryBreakdownReportAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new SpentInCategoryReport { Category = "Groceries", TotalOut = -300m },
                new SpentInCategoryReport { Category = "Takeaways", TotalOut = -450m },
                new SpentInCategoryReport { Category = "Salary", TotalIn = 2000m }
            }.ToAsyncEnumerable());

        // Act
        JsonElement breakdown = await CallAsync("get_spending_breakdown", new { by = "Tag" });

        // Assert
        List<string> names = breakdown.GetProperty("groups").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString()!)
            .ToList();
        await Assert.That(names).IsEquivalentTo(["Takeaways", "Groceries", "Salary"], CollectionOrdering.Matching);
        await Assert.That(breakdown.GetProperty("link").GetString()).IsEqualTo("/Reports/tags");
    }

    [Test]
    public async Task GetSpendingBreakdown_WithManyGroups_ShowsFiftyAndSaysThereAreMore()
    {
        // Arrange
        IEnumerable<SpentInAccountReport> accounts = Enumerable.Range(1, 60)
            .Select(x => new SpentInAccountReport { AccountName = $"Account {x}", TotalOut = -x });
        _reportService.GetAccountBreakdownReportAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(accounts.ToAsyncEnumerable());

        // Act
        JsonElement breakdown = await CallAsync("get_spending_breakdown", new { by = "Account" });

        // Assert
        await Assert.That(breakdown.GetProperty("groups").GetArrayLength()).IsEqualTo(50);
        await Assert.That(breakdown.GetProperty("moreThanShown").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task ComparePeriods_GivesTheChangeFromTheFirstToTheSecond()
    {
        // Arrange
        _reportService.GetSpentInTimePeriodReportAsync(
                Arg.Is<FilterTransactions>(x => x.FromDate!.Value.Year == 2025), Arg.Any<CancellationToken>())
            .Returns(new[] { new SpentInTimePeriodReport { TotalIn = 1000m, TotalOut = -400m } }.ToAsyncEnumerable());
        _reportService.GetSpentInTimePeriodReportAsync(
                Arg.Is<FilterTransactions>(x => x.FromDate!.Value.Year == 2026), Arg.Any<CancellationToken>())
            .Returns(new[] { new SpentInTimePeriodReport { TotalIn = 1100m, TotalOut = -550m } }.ToAsyncEnumerable());

        // Act
        JsonElement comparison = await CallAsync("compare_periods", new
        {
            firstFrom = "2025-01-01", firstTo = "2025-06-15", secondFrom = "2026-01-01", secondTo = "2026-06-15"
        });

        // Assert
        await Assert.That(comparison.GetProperty("moneyInChange").GetDecimal()).IsEqualTo(100m);
        await Assert.That(comparison.GetProperty("moneyOutChange").GetDecimal()).IsEqualTo(150m);
    }
}
