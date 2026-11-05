using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Infrastructure.Exports.Statements;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Exports;

public class StatementTests
{
    [Test]
    public async Task Untagged_IsSpendingWithNoTags()
    {
        // Arrange
        Statement statement = With(
            TransactionCsvTests.Transaction("CORNER SHOP", -12.50m),
            TransactionCsvTests.Transaction("BAKERY", -3m),
            TransactionCsvTests.Transaction("SUPERMARKET", -40m, ["Groceries"]));

        // Act
        decimal untagged = statement.Untagged;

        // Assert
        await Assert.That(untagged).IsEqualTo(15.50m);
    }

    [Test]
    public async Task Untagged_LeavesOutMoneyInAndTransfers()
    {
        // Arrange
        Statement statement = With(
            TransactionCsvTests.Transaction("EMPLOYER LTD", 2000m),
            TransactionCsvTests.Transaction("TO SAVINGS", -300m, transfer: TransferState.Transfer),
            TransactionCsvTests.Transaction("CORNER SHOP", -5m));

        // Act
        decimal untagged = statement.Untagged;

        // Assert
        await Assert.That(untagged).IsEqualTo(5m);
    }

    [Test]
    public async Task Untagged_OfASplit_IsItsUntaggedParts()
    {
        // Arrange: £100 split into £60 of groceries and £40 untagged
        Statement statement = With(TransactionCsvTests.Transaction("SUPERMARKET", -100m, ["Groceries"],
            splits:
            [
                new TransactionSplitPart { Amount = -60m, TagId = Guid.NewGuid(), Tag = "Groceries" },
                new TransactionSplitPart { Amount = -40m }
            ]));

        // Act
        decimal untagged = statement.Untagged;

        // Assert
        await Assert.That(untagged).IsEqualTo(40m);
    }

    private static Statement With(params Transaction[] transactions) => new()
    {
        Month = new DateOnly(2026, 11, 1),
        Start = new DateOnly(2026, 11, 1),
        End = new DateOnly(2026, 11, 30),
        IsCurrent = false,
        MadeOn = new DateOnly(2026, 12, 1),
        Totals = new SpentInTimePeriod(),
        Tags = [],
        Bills = new BillMonthOverview(),
        Budgets = new BudgetMonthOverview(),
        SavingsGoals = [],
        Debts = [],
        Transactions = transactions
    };
}
