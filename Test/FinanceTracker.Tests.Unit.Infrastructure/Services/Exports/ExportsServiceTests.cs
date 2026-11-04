using System.Text;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Infrastructure.Exports;
using FinanceTracker.Tests.Unit.Shared;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Exports;

public class ExportsServiceTests : UnitTestBase
{
    private readonly ITransactionsService _transactionsService = Substitute.For<ITransactionsService>();

    [Test]
    public async Task WriteTransactionsCsvAsync_WritesTheHeaderThenAWindowsLineForEachTransactionTheFiltersFind()
    {
        // Arrange
        FilterTransactions filter = new() { SearchTerm = "shop" };
        _transactionsService.GetAllTransactionsAsync(filter, Arg.Any<CancellationToken>()).Returns(new[]
        {
            TransactionCsvTests.Transaction("SHOP ONE", -1m),
            TransactionCsvTests.Transaction("SHOP TWO", -2m)
        }.ToAsyncEnumerable());
        using MemoryStream destination = new();

        // Act
        await BuildSubject().WriteTransactionsCsvAsync(filter, destination, CancellationToken);

        // Assert
        string[] lines = TransactionCsvTests.Lines(destination.ToArray());
        using (Assert.Multiple())
        {
            await Assert.That(lines.Length).IsEqualTo(3);
            await Assert.That(lines[0]).IsEqualTo(TransactionCsvTests.Header);
            await Assert.That(lines[1]).Contains("SHOP ONE");
            await Assert.That(lines[2]).Contains("SHOP TWO");
        }
    }

    [Test]
    public async Task WriteTransactionsCsvAsync_NothingFound_IsJustTheHeader()
    {
        // Arrange
        _transactionsService.GetAllTransactionsAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<Transaction>());
        using MemoryStream destination = new();

        // Act
        await BuildSubject().WriteTransactionsCsvAsync(new FilterTransactions(), destination, CancellationToken);

        // Assert
        await Assert.That(Encoding.UTF8.GetString(destination.ToArray()).TrimStart('﻿'))
            .IsEqualTo(TransactionCsvTests.Header + "\r\n");
    }

    private ExportsService BuildSubject() =>
        new(_transactionsService, new FixedTimeProvider(DateTime.UtcNow));
}
