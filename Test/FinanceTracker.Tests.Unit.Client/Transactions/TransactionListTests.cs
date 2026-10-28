using Bunit;
using FinanceTracker.Client.Components.Transactions;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Tests.Unit.Client.Transactions;

public class TransactionListTests : ComponentTestBase
{
    [Test]
    public async Task Render_ShowsWhatWhereAndHowEachCounts()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        IRenderedComponent<TransactionList> cut = Render<TransactionList>(p => p
            .Add(x => x.Transactions, [Transaction("Coffee", -3.5m, pending: true)]));

        // Assert
        string row = cut.Find(".ft-transaction").TextContent;

        using (Assert.Multiple())
        {
            await Assert.That(row).Contains("Coffee");
            await Assert.That(row).Contains("Current · 4 Oct 2026 · Pending");
            await Assert.That(row).Contains("Spending");
        }
    }

    [Test]
    public async Task Render_MoreThanAPage_ShowsAPageThenMoreOnRequest()
    {
        // Arrange
        List<Transaction> transactions = Enumerable.Range(0, TransactionList.PageSize + 5)
            .Select(static x => Transaction($"Transaction {x}", -1m))
            .ToList();
        IRenderedComponent<TransactionList> cut = Render<TransactionList>(p => p.Add(x => x.Transactions, transactions));
        int firstPage = cut.FindAll(".ft-transaction").Count;

        // Act
        cut.Find(".ft-transaction-list__more button").Click();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(firstPage).IsEqualTo(TransactionList.PageSize);
            await Assert.That(cut.FindAll(".ft-transaction")).Count().IsEqualTo(transactions.Count);
            await Assert.That(cut.FindAll(".ft-transaction-list__more")).IsEmpty();
        }
    }

    [Test]
    public async Task Tap_RaisesTheTransaction()
    {
        // Arrange
        Transaction coffee = Transaction("Coffee", -3.5m);
        Transaction? selected = null;
        IRenderedComponent<TransactionList> cut = Render<TransactionList>(p => p
            .Add(x => x.Transactions, [coffee])
            .Add(x => x.OnSelect, (Transaction x) => selected = x));

        // Act
        cut.Find(".ft-transaction").Click();

        // Assert
        await Assert.That(selected).IsSameReferenceAs(coffee);
    }

    [Test]
    public async Task Render_None_SaysSo()
    {
        // Act
        IRenderedComponent<TransactionList> cut = Render<TransactionList>(p => p.Add(x => x.Transactions, []));

        // Assert
        await Assert.That(cut.Markup).Contains("No transactions match these filters.");
    }

    internal static Transaction Transaction(string description, decimal amount, bool pending = false,
        TransactionTransfer? transfer = null) => new()
    {
        TransactionId = Guid.NewGuid(),
        Description = description,
        TransactionType = amount < 0 ? "Debit" : "Credit",
        TransactionCategory = "PURCHASE",
        Amount = amount,
        Currency = "GBP",
        TransactionTime = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc),
        Pending = pending,
        Tags = [],
        AccountName = "Current",
        Transfer = transfer
    };
}
