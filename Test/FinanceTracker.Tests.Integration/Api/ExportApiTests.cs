using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FinanceTracker.Contracts.Exports;
using FinanceTracker.Contracts.PayPeriods;
using FinanceTracker.Contracts.Reports;
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

    [Test]
    public async Task Backup_IsAZipOfOnlyTheUsersOwnData()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        SignedInUser other = await RegisterUserAsync();
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("mine", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "mine"))
            .Do(s => s.TransactionScenario.WithTransaction("coffee", "main",
                new TransactionDetailsFaker().Generate() with { Description = "COFFEE SHOP" }))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", other.Username))
            .Do(s => s.BankAccountScenario.WithAccount("their-account", "theirs"))
            .Do(s => s.TransactionScenario.WithTransaction("their-rent", "their-account",
                new TransactionDetailsFaker().Generate() with { Description = "THEIR RENT" }));
        IExportsService service = ApiFor(user).Get<IExportsService>();
        using MemoryStream backup = new();

        // Act
        await service.WriteBackupAsync(backup, CancellationToken);

        // Assert
        backup.Position = 0;
        using ZipArchive zip = new(backup, ZipArchiveMode.Read);
        using JsonDocument transactions = JsonDocument.Parse(Read(zip, "transactions.json"));
        using JsonDocument accounts = JsonDocument.Parse(Read(zip, "accounts.json"));
        using JsonDocument manifest = JsonDocument.Parse(Read(zip, "manifest.json"));
        using (Assert.Multiple())
        {
            await Assert.That(transactions.RootElement.EnumerateArray()
                    .Select(static x => x.GetProperty("description").GetString()!))
                .IsEquivalentTo(["COFFEE SHOP"]);
            await Assert.That(accounts.RootElement.GetArrayLength()).IsEqualTo(1);
            await Assert.That(Read(zip, "transactions.csv")).DoesNotContain("THEIR RENT");
            await Assert.That(manifest.RootElement.GetProperty("files").GetProperty("transactions.json").GetInt32())
                .IsEqualTo(1);
        }
    }

    [Test]
    public async Task StatementPeriods_WithNoTransactions_IsJustTodays()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IExportsService service = ApiFor(user).Get<IExportsService>();
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);

        // Act
        List<PayPeriod> periods = await service.GetStatementPeriodsAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(periods).Count().IsEqualTo(1);
            await Assert.That(periods[0].Start).IsLessThanOrEqualTo(today);
            await Assert.That(periods[0].End).IsGreaterThanOrEqualTo(today);
        }
    }

    [Test]
    public async Task StatementPeriods_GoBackToTheOldestTransactionNewestFirst()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        DateTime oldest = DateTime.UtcNow.AddMonths(-3);
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("mine", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "mine"))
            .Do(s => s.TransactionScenario.WithTransaction("old", "main",
                new TransactionDetailsFaker().Generate() with { OccurredAt = oldest }))
            .Do(s => s.TransactionScenario.WithTransaction("recent", "main",
                new TransactionDetailsFaker().Generate() with { OccurredAt = DateTime.UtcNow.AddDays(-1) }));
        IExportsService service = ApiFor(user).Get<IExportsService>();
        DateOnly oldestDay = DateOnly.FromDateTime(oldest.ToLocalTime());

        // Act
        List<PayPeriod> periods = await service.GetStatementPeriodsAsync(CancellationToken);

        // Assert: four calendar months, as the user has no pay, the last holding the oldest transaction
        using (Assert.Multiple())
        {
            await Assert.That(periods.Select(static x => x.Month)).IsOrderedByDescending(static x => x);
            await Assert.That(periods[^1].Start).IsLessThanOrEqualTo(oldestDay);
            await Assert.That(periods[^1].End).IsGreaterThanOrEqualTo(oldestDay);
            await Assert.That(periods.Count).IsBetween(4, 5);
        }
    }

    [Test]
    public async Task Statement_IsAPdf()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("mine", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "mine"))
            .Do(s => s.TransactionScenario.WithTransaction("coffee", "main",
                new TransactionDetailsFaker().Generate() with { Description = "COFFEE SHOP" }));
        IExportsService service = ApiFor(user).Get<IExportsService>();
        DateTime now = DateTime.UtcNow;
        using MemoryStream pdf = new();

        // Act
        await service.WriteStatementAsync(now.Year, now.Month, pdf, CancellationToken);

        // Assert
        await Assert.That(Encoding.ASCII.GetString(pdf.ToArray(), 0, 5)).IsEqualTo("%PDF-");
    }

    [Test]
    [Arguments(ReportBreakdown.OverTime)]
    [Arguments(ReportBreakdown.ByAccount)]
    [Arguments(ReportBreakdown.ByTag)]
    public async Task Report_IsAPdf(ReportBreakdown breakdown)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("mine", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "mine"))
            .Do(s => s.TransactionScenario.WithTransaction("coffee", "main",
                new TransactionDetailsFaker().Generate() with { Description = "COFFEE SHOP" }));
        IExportsService service = ApiFor(user).Get<IExportsService>();
        using MemoryStream pdf = new();

        // Act
        await service.WriteReportAsync(new ExportReport { Breakdown = breakdown }, pdf, CancellationToken);

        // Assert
        await Assert.That(Encoding.ASCII.GetString(pdf.ToArray(), 0, 5)).IsEqualTo("%PDF-");
    }

    private static string Read(ZipArchive zip, string name)
    {
        using StreamReader reader = new(zip.GetEntry(name)!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
