using FinanceTracker.Contracts.Reports.Account;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.Reports.SpentInTimePeriod;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;

namespace FinanceTracker.Tests.Integration.Api;

/// <summary>
/// The reports through the API, each for October 2026, with a payment outside it to leave out.
/// </summary>
public class ReportApiTests : IntegrationTestBase
{
    private static readonly FilterTransactions October = new()
    {
        FromDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        ToDate = new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc)
    };

    [Test]
    public async Task SpentInTimePeriod_ForAMonth_TotalsItsTransactions()
    {
        // Arrange
        SignedInUser user = await SeedAsync();
        IReportService service = ApiFor(user).Get<IReportService>();

        // Act
        List<SpentInTimePeriodReport> report = await service.GetSpentInTimePeriodReportAsync(October,
            CancellationToken).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Sum(static x => x.TotalTransactions)).IsEqualTo(3);
            await Assert.That(report.Sum(static x => x.TotalIn)).IsEqualTo(1200m);
            await Assert.That(report.Sum(static x => x.TotalOut)).IsEqualTo(-65.5m);
        }
    }

    [Test]
    public async Task AccountBreakdown_ForAMonth_SplitsItByAccount()
    {
        // Arrange
        SignedInUser user = await SeedAsync();
        IReportService service = ApiFor(user).Get<IReportService>();

        // Act
        List<SpentInAccountReport> report = await service.GetAccountBreakdownReportAsync(October, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        Dictionary<Guid, int> counts = report.ToDictionary(static x => x.AccountId, static x => x.TotalTransactions);
        using (Assert.Multiple())
        {
            await Assert.That(counts[Scenario.AccountMappings["current"]]).IsEqualTo(2);
            await Assert.That(counts[Scenario.AccountMappings["card"]]).IsEqualTo(1);
        }
    }

    [Test]
    public async Task CategoryBreakdown_ForAMonth_SplitsItByTheBanksClassification()
    {
        // Arrange
        SignedInUser user = await SeedAsync();
        IReportService service = ApiFor(user).Get<IReportService>();

        // Act
        List<SpentInCategoryReport> report = await service.GetCategoryBreakdownReportAsync(October, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        SpentInCategoryReport shopping = report.Single(static x => x.Category == "Shopping");
        using (Assert.Multiple())
        {
            await Assert.That(shopping.TotalTransactions).IsEqualTo(2);
            await Assert.That(shopping.TotalOut).IsEqualTo(-65.5m);
        }
    }

    /// <summary>
    /// Pay and a shop in the current account, a shop on the card, all in October, and a shop in September.
    /// </summary>
    private async Task<SignedInUser> SeedAsync()
    {
        SignedInUser user = await RegisterUserAsync();
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("current", "bank"))
            .Do(s => s.BankAccountScenario.WithAccount("card", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("pay", "current", Details("ACME LTD", 1200m, 10, 1)))
            .Do(s => s.TransactionScenario.WithTransaction("shop", "current",
                Details("CORNER SHOP", -45.5m, 10, 12, "Shopping")))
            .Do(s => s.TransactionScenario.WithTransaction("online", "card",
                Details("ONLINE SHOP", -20m, 10, 20, "Shopping")))
            .Do(s => s.TransactionScenario.WithTransaction("before", "current",
                Details("CORNER SHOP", -9m, 9, 28, "Shopping")));
        return user;
    }

    private static TransactionDetails Details(string description, decimal amount, int month, int day,
        params string[] classifications)
    {
        TransactionType type = TransactionType.Debit;
        if (amount > 0)
        {
            type = TransactionType.Credit;
        }

        return new TransactionDetails($"tx-{Guid.NewGuid():N}", description, type, "PURCHASE", amount, "GBP",
            new DateTime(2026, month, day, 12, 0, 0, DateTimeKind.Utc), false, classifications,
            TransactionMetadata.None, false);
    }
}
