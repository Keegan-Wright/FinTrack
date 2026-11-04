using System.Text;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;

namespace FinanceTracker.Tests.Integration.Api;

public class ExportApiTests : IntegrationTestBase
{
    [Test]
    public async Task ExportTransactions_HasOnlyTheUsersOwnTransactionsTheFiltersFind()
    {
        // Arrange: two of the user's, one someone else's that the search would match
        SignedInUser user = await RegisterUserAsync();
        SignedInUser other = await RegisterUserAsync();
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("mine", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "mine"))
            .Do(s => s.TransactionScenario.WithTransaction("coffee", "main",
                new TransactionDetailsFaker().Generate() with { Description = "COFFEE SHOP" }))
            .Do(s => s.TransactionScenario.WithTransaction("rent", "main",
                new TransactionDetailsFaker().Generate() with { Description = "RENT" }))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", other.Username))
            .Do(s => s.BankAccountScenario.WithAccount("their-account", "theirs"))
            .Do(s => s.TransactionScenario.WithTransaction("their-coffee", "their-account",
                new TransactionDetailsFaker().Generate() with { Description = "COFFEE SHOP TOO" }));
        IExportsService service = ApiFor(user).Get<IExportsService>();
        using MemoryStream csv = new();

        // Act
        await service.WriteTransactionsCsvAsync(new FilterTransactions { SearchTerm = "coffee" }, csv,
            CancellationToken);

        // Assert
        string[] lines = Encoding.UTF8.GetString(csv.ToArray()).TrimStart('﻿')
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        using (Assert.Multiple())
        {
            await Assert.That(lines[0]).StartsWith("Date,Account,Description,Amount");
            await Assert.That(lines.Length).IsEqualTo(2);
            await Assert.That(lines[1]).Contains("COFFEE SHOP");
            await Assert.That(lines[1]).DoesNotContain("COFFEE SHOP TOO");
        }
    }
}
