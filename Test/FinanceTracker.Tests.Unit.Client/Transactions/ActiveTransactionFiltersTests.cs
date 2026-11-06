using FinanceTracker.Client.Components.Transactions;
using FinanceTracker.Contracts.Transactions;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.Transactions;

public class ActiveTransactionFiltersTests
{
    [Test]
    public async Task ToRequest_WithADateRange_RunsFromTheStartOfTheFirstDayToTheEndOfTheLast()
    {
        // Arrange
        ActiveTransactionFilters filters = ActiveTransactionFilters.None with
        {
            DateRange = new DateRange(new DateTime(2026, 8, 1, 9, 0, 0), new DateTime(2026, 8, 31))
        };

        // Act
        FilterTransactions request = filters.ToRequest(TimeZoneInfo.Utc);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(request.FromDate).IsEqualTo(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
            await Assert.That(request.ToDate)
                .IsEqualTo(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1));
        }
    }

    [Test]
    public async Task ToRequest_InSummerTime_RunsBetweenMidnightsWhereTheUserIs()
    {
        // Arrange, August in the UK is on BST, an hour ahead of UTC
        TimeZoneInfo london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        ActiveTransactionFilters filters = ActiveTransactionFilters.None with
        {
            DateRange = new DateRange(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31))
        };

        // Act
        FilterTransactions request = filters.ToRequest(london);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(request.FromDate).IsEqualTo(new DateTime(2026, 7, 31, 23, 0, 0, DateTimeKind.Utc));
            await Assert.That(request.ToDate)
                .IsEqualTo(new DateTime(2026, 8, 31, 23, 0, 0, DateTimeKind.Utc).AddTicks(-1));
        }
    }

    [Test]
    public async Task ToRequest_WithNoDates_LeavesThemOpen()
    {
        // Act
        FilterTransactions request = ActiveTransactionFilters.None.ToRequest(TimeZoneInfo.Utc);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(request.FromDate).IsNull();
            await Assert.That(request.ToDate).IsNull();
        }
    }

    [Test]
    public async Task ToRequest_CopiesEveryFilter()
    {
        // Arrange
        Guid providerId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Guid billId = Guid.NewGuid();
        Guid debtId = Guid.NewGuid();
        Guid goalId = Guid.NewGuid();
        ActiveTransactionFilters filters = new([providerId], [accountId], ["PURCHASE"], ["Debit"], ["Food"],
            new DateRange(null, new DateTime(2026, 8, 31)), "coffee", [billId], [debtId], [goalId]);

        // Act
        FilterTransactions request = filters.ToRequest(TimeZoneInfo.Utc);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(request.ProviderIds!).IsEquivalentTo([providerId]);
            await Assert.That(request.AccountIds!).IsEquivalentTo([accountId]);
            await Assert.That(request.Categories!).IsEquivalentTo(["PURCHASE"]);
            await Assert.That(request.Types!).IsEquivalentTo(["Debit"]);
            await Assert.That(request.Tags!).IsEquivalentTo(["Food"]);
            await Assert.That(request.SearchTerm).IsEqualTo("coffee");
            await Assert.That(request.FromDate).IsNull();
            await Assert.That(request.ToDate)
                .IsEqualTo(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1));
            await Assert.That(request.BillIds!).IsEquivalentTo([billId]);
            await Assert.That(request.DebtIds!).IsEquivalentTo([debtId]);
            await Assert.That(request.SavingsGoalIds!).IsEquivalentTo([goalId]);
        }
    }
}
