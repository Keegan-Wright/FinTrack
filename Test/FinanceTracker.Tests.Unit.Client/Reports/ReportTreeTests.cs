using System.Collections.Immutable;
using FinanceTracker.Client.Components.Reports;
using FinanceTracker.Contracts.Reports.Account;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.Reports.SpentInTimePeriod;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Tests.Unit.Client.Reports;

public class ReportTreeTests
{
    private static readonly Guid AccountId = Guid.NewGuid();

    [Test]
    public async Task From_SpentInTimePeriodReport_FillsEveryMonthAndDayOfTheYear()
    {
        // Arrange
        using CultureScope culture = new();
        SpentInTimePeriodReport report = new()
        {
            TotalIn = 100m,
            TotalOut = -40m,
            TotalTransactions = 3,
            YearlyBreakdown =
            [
                new SpentInTimePeriodReportYearlyBreakdown
                {
                    Year = 2026,
                    TotalIn = 100m,
                    TotalOut = -40m,
                    TotalTransactions = 3,
                    MonthlyBreakdown =
                    [
                        new SpentInTimePeriodReportMonthlyBreakdown
                        {
                            Month = "February",
                            TotalIn = 100m,
                            TotalOut = -40m,
                            TotalTransactions = 3,
                            DailyBreakdown =
                            [
                                new SpentInTimePeriodReportDailyBreakdown
                                {
                                    Day = 14, TotalIn = 100m, TotalOut = -40m, TotalTransactions = 3
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        // Act
        ReportNode root = ReportTree.From(report, new FilterTransactions());

        // Assert
        ReportNode year = root.Children.Single();
        ReportNode february = year.Children[1];
        ReportNode day = february.Children[13];
        using (Assert.Multiple())
        {
            await Assert.That(root.Title).IsEqualTo("All accounts");
            await Assert.That(root.Net).IsEqualTo(60m);
            await Assert.That(year.Children.Count).IsEqualTo(12);
            await Assert.That(year.Children[0].TotalTransactions).IsEqualTo(0);
            await Assert.That(february.Title).IsEqualTo("February 2026");
            await Assert.That(february.Children.Count).IsEqualTo(28);
            await Assert.That(day.Date).IsEqualTo(new DateTime(2026, 2, 14));
            await Assert.That(day.TotalIn).IsEqualTo(100m);
            await Assert.That(day.CanDrillInto).IsTrue();
            await Assert.That(february.Children[0].CanDrillInto).IsFalse();
        }
    }

    [Test]
    public async Task From_SpentInTimePeriodReport_KeepsTheFiltersAndLeavesOutTransfers()
    {
        // Arrange
        FilterTransactions request = new() { SearchTerm = "coffee", Tags = ["Food"] };

        // Act
        ReportNode root = ReportTree.From(new SpentInTimePeriodReport(), request);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(root.Scope).IsSameReferenceAs(request);
            await Assert.That(root.IncludesTransfers).IsFalse();
            await Assert.That(root.AccountId).IsNull();
        }
    }

    [Test]
    public async Task From_SpentInAccountReport_NarrowsToTheAccountAndCountsTransfers()
    {
        // Arrange
        FilterTransactions request = new()
        {
            AccountIds = [Guid.NewGuid(), AccountId], SearchTerm = "coffee", FromDate = new DateTime(2026, 1, 1)
        };
        SpentInAccountReport report = new() { AccountId = AccountId, AccountName = "Bills" };

        // Act
        ReportNode root = ReportTree.From(report, request);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(root.Title).IsEqualTo("Bills");
            await Assert.That(root.AccountId).IsEqualTo(AccountId);
            await Assert.That(root.IncludesTransfers).IsTrue();
            await Assert.That(root.Scope!.AccountIds!).IsEquivalentTo([AccountId]);
            await Assert.That(root.Scope.SearchTerm).IsEqualTo("coffee");
            await Assert.That(root.Scope.FromDate).IsEqualTo(new DateTime(2026, 1, 1));
        }
    }

    [Test]
    public async Task From_SpentInCategoryReport_NarrowsToTheTag()
    {
        // Arrange
        FilterTransactions request = new() { Tags = ["Food", "Travel"], AccountIds = [AccountId] };
        SpentInCategoryReport report = new() { Category = "Travel" };

        // Act
        ReportNode root = ReportTree.From(report, request);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(root.Scope!.Tags!).IsEquivalentTo(["Travel"]);
            await Assert.That(root.Scope.AccountIds!).IsEquivalentTo([AccountId]);
            await Assert.That(root.IncludesTransfers).IsFalse();
        }
    }

    [Test]
    public async Task ForDay_CoversTheWholeDayAndKeepsTheScope()
    {
        // Arrange
        FilterTransactions scope = new()
        {
            AccountIds = [AccountId], FromDate = new DateTime(2026, 1, 1), ToDate = new DateTime(2026, 12, 31)
        };

        // Act
        FilterTransactions request = ReportTree.ForDay(scope, new DateTime(2026, 8, 14, 15, 30, 0));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(request.FromDate).IsEqualTo(new DateTime(2026, 8, 14));
            await Assert.That(request.ToDate).IsEqualTo(new DateTime(2026, 8, 15).AddTicks(-1));
            await Assert.That(request.AccountIds!).IsEquivalentTo([AccountId]);
        }
    }

    [Test]
    public async Task EmptyAccounts_AddsTheAccountsWithoutTransactions()
    {
        // Arrange
        Guid emptyId = Guid.NewGuid();
        ReportNode reported = ReportTree.From(new SpentInAccountReport { AccountId = AccountId, AccountName = "Bills" },
            new FilterTransactions());
        TransactionAccountFilter[] accounts =
        [
            new() { AccountId = AccountId, AccountName = "Bills" },
            new() { AccountId = emptyId, AccountName = "Savings" }
        ];

        // Act
        List<ReportNode> empty = ReportTree.EmptyAccounts([reported], accounts, new FilterTransactions()).ToList();

        // Assert
        ReportNode savings = empty.Single();
        using (Assert.Multiple())
        {
            await Assert.That(savings.AccountId).IsEqualTo(emptyId);
            await Assert.That(savings.Title).IsEqualTo("Savings");
            await Assert.That(savings.TotalTransactions).IsEqualTo(0);
            await Assert.That(savings.CanDrillInto).IsFalse();
        }
    }

    [Test]
    public async Task EmptyAccounts_OnlyAddsTheChosenAccounts()
    {
        // Arrange
        Guid chosenId = Guid.NewGuid();
        TransactionAccountFilter[] accounts =
        [
            new() { AccountId = AccountId, AccountName = "Bills" },
            new() { AccountId = chosenId, AccountName = "Savings" }
        ];
        FilterTransactions request = new() { AccountIds = [chosenId] };

        // Act
        List<ReportNode> empty = ReportTree.EmptyAccounts([], accounts, request).ToList();

        // Assert
        await Assert.That(empty.Select(static x => x.AccountId)).IsEquivalentTo(new Guid?[] { chosenId });
    }

    [Test]
    public async Task EmptyAccounts_WhenABankIsChosen_AddsNone()
    {
        // Arrange
        TransactionAccountFilter[] accounts = [new() { AccountId = AccountId, AccountName = "Bills" }];
        FilterTransactions request = new() { ProviderIds = ImmutableList.Create(Guid.NewGuid()) };

        // Act
        List<ReportNode> empty = ReportTree.EmptyAccounts([], accounts, request).ToList();

        // Assert
        await Assert.That(empty).IsEmpty();
    }
}
