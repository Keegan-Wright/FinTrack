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
        FilterTransactions request = filters.ToRequest();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(request.FromDate).IsEqualTo(new DateTime(2026, 8, 1));
            await Assert.That(request.ToDate).IsEqualTo(new DateTime(2026, 9, 1).AddTicks(-1));
        }
    }

    [Test]
    public async Task ToRequest_WithNoDates_LeavesThemOpen()
    {
        // Act
        FilterTransactions request = ActiveTransactionFilters.None.ToRequest();

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
        ActiveTransactionFilters filters = new([providerId], [accountId], ["PURCHASE"], ["Debit"], ["Food"],
            new DateRange(null, new DateTime(2026, 8, 31)), "coffee");

        // Act
        FilterTransactions request = filters.ToRequest();

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
            await Assert.That(request.ToDate).IsEqualTo(new DateTime(2026, 9, 1).AddTicks(-1));
        }
    }
}
