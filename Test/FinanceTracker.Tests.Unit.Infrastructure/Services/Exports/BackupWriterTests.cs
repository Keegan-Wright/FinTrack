using System.IO.Compression;
using System.Text.Json;
using FinanceTracker.Contracts.Account;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Calendar;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Exports;
using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Contracts.NetWorth;
using FinanceTracker.Contracts.PayPeriods;
using FinanceTracker.Contracts.Rules;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Infrastructure.Common.Exports;
using FinanceTracker.Infrastructure.Exports;
using FinanceTracker.Tests.Unit.Shared;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Exports;

public class BackupWriterTests : UnitTestBase
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    private const string Password = "Placeholder backup password";

    private static readonly string[] Files =
    [
        "accounts.json", "transactions.json", "transactions.csv", "bills.json", "budgets.json", "debts.json",
        "savings-goals.json", "rules.json", "tags.json", "calendar-events.json", "household.json", "pay.json",
        "net-worth.json", "balance-history.json"
    ];

    private readonly ITransactionsService _transactionsService = Substitute.For<ITransactionsService>();
    private readonly IAccountService _accountService = Substitute.For<IAccountService>();

    public BackupWriterTests()
    {
        _transactionsService.GetAllTransactionsAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(_ => new[]
            {
                TransactionCsvTests.Transaction("COFFEE SHOP", -3.5m),
                TransactionCsvTests.Transaction("RENT", -800m)
            }.ToAsyncEnumerable());
        _accountService.GetAccountsAndMostRecentTransactionsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<AccountAndTransactions>());
    }

    [Test]
    public async Task WriteAsync_HasAFileForEachKindOfDataAndAManifestCountingThem()
    {
        // Act
        using ZipArchive zip = await WriteAsync();

        // Assert
        BackupManifest manifest = Read<BackupManifest>(zip, "manifest.json");
        using (Assert.Multiple())
        {
            await Assert.That(zip.Entries.Select(static x => x.FullName)).IsEquivalentTo([.. Files, "manifest.json"]);
            await Assert.That(manifest.Format).IsEqualTo("fintrack-backup");
            await Assert.That(manifest.FormatVersion).IsEqualTo(1);
            await Assert.That(manifest.ExportedAt).IsEqualTo(new DateTimeOffset(Now));
            await Assert.That(manifest.Files.Keys).IsEquivalentTo(Files);
            await Assert.That(manifest.Files["transactions.json"]).IsEqualTo(2);
            await Assert.That(manifest.Files["transactions.csv"]).IsEqualTo(2);
            await Assert.That(manifest.Files["bills.json"]).IsEqualTo(0);
            await Assert.That(manifest.Files["pay.json"]).IsEqualTo(1);
        }
    }

    [Test]
    public async Task WriteAsync_WithAModuleSection_AddsItsFileEncryptedAndCountsIt()
    {
        // Arrange
        IBackupSection section = Substitute.For<IBackupSection>();
        section.FileName.Returns("module.json");
        section.ReadAsync(Arg.Any<CancellationToken>())
            .Returns(new BackupSectionData(new { Setting = "On" }, Records: 1));
        MemoryStream backup = new();

        // Act
        await BuildWriter(_transactionsService, _accountService, section)
            .WriteAsync(backup, Password, CancellationToken);

        // Assert
        backup.Position = 0;
        using ZipArchive zip = new(backup, ZipArchiveMode.Read);
        BackupManifest manifest = JsonSerializer.Deserialize<BackupManifest>(
            Bytes(zip, "manifest.json", Password), JsonSerializerOptions.Web)!;
        using (Assert.Multiple())
        {
            await Assert.That(zip.GetEntry("module.json")!.EncryptionMethod).IsEqualTo(ZipEncryptionMethod.Aes256);
            await Assert.That(manifest.Files["module.json"]).IsEqualTo(1);
            await Assert.That(System.Text.Encoding.UTF8.GetString(Bytes(zip, "module.json", Password)))
                .Contains("\"setting\": \"On\"");
        }
    }

    [Test]
    public async Task WriteAsync_WithAPassword_EveryFileIsAes256AndOpensOnlyWithIt()
    {
        // Act
        using ZipArchive zip = await WriteAsync(Password);

        // Assert
        ZipArchiveEntry manifest = zip.GetEntry("manifest.json")!;
        using (Assert.Multiple())
        {
            await Assert.That(zip.Entries.Select(static x => x.EncryptionMethod).Distinct())
                .IsEquivalentTo([ZipEncryptionMethod.Aes256]);
            await Assert.That(JsonSerializer.Deserialize<BackupManifest>(Bytes(zip, "manifest.json", Password),
                JsonSerializerOptions.Web)!.Files).Count().IsEqualTo(Files.Length);
            await Assert.That(() => manifest.Open("Placeholder-wrong-password")).Throws<InvalidDataException>();
            await Assert.That(() => manifest.Open()).Throws<InvalidDataException>();
        }
    }

    [Test]
    public async Task WriteAsync_WithoutAPassword_NoFileIsEncrypted()
    {
        // Act
        using ZipArchive zip = await WriteAsync();

        // Assert
        await Assert.That(zip.Entries.Select(static x => x.EncryptionMethod).Distinct())
            .IsEquivalentTo([ZipEncryptionMethod.None]);
    }

    [Test]
    public async Task WriteAsync_IntoAStreamThatRefusesSynchronousWrites_WritesTheWholeZip()
    {
        // Arrange, as an HTTP response body is, which the backup is written straight into
        AsyncWritesOnlyStream response = new();

        // Act
        await BuildWriter(_transactionsService, _accountService).WriteAsync(response, Password, CancellationToken);

        // Assert
        response.Written.Position = 0;
        using ZipArchive zip = new(response.Written, ZipArchiveMode.Read);
        await Assert.That(zip.Entries.Select(static x => x.FullName)).IsEquivalentTo([.. Files, "manifest.json"]);
    }

    [Test]
    public async Task WriteAsync_HasEveryTransactionAsJsonAndAsTheSpreadsheet()
    {
        // Act
        using ZipArchive zip = await WriteAsync();

        // Assert
        using JsonDocument transactions = JsonDocument.Parse(Text(zip, "transactions.json"));
        string[] csv = TransactionCsvTests.Lines(Bytes(zip, "transactions.csv"));
        using (Assert.Multiple())
        {
            await Assert.That(transactions.RootElement.EnumerateArray()
                    .Select(static x => x.GetProperty("description").GetString()!))
                .IsEquivalentTo(["COFFEE SHOP", "RENT"]);
            await Assert.That(csv[0]).IsEqualTo(TransactionCsvTests.Header);
            await Assert.That(csv.Length).IsEqualTo(3);
        }

        // Every transaction, not just a page's filters
        _transactionsService.Received(2).GetAllTransactionsAsync(
            Arg.Is<FilterTransactions>(static x => x.SearchTerm == null), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task WriteAsync_AccountsLeaveTheirTransactionsToTheirOwnFile()
    {
        // Act
        using ZipArchive zip = await WriteAsync();

        // Assert
        _accountService.Received(1).GetAccountsAndMostRecentTransactionsAsync(0, Arg.Any<CancellationToken>());
        await Assert.That(Text(zip, "accounts.json").Trim()).IsEqualTo("[]");
    }

    /// <summary>
    /// A <see cref="BackupWriter"/> whose services have nothing, apart from those given.
    /// </summary>
    internal static BackupWriter BuildWriter(ITransactionsService? transactions = null,
        IAccountService? accounts = null, params IBackupSection[] sections)
    {
        if (accounts is null)
        {
            accounts = Substitute.For<IAccountService>();
            accounts.GetAccountsAndMostRecentTransactionsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(_ => AsyncEnumerable.Empty<AccountAndTransactions>());
        }

        if (transactions is null)
        {
            transactions = Substitute.For<ITransactionsService>();
            transactions.GetAllTransactionsAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
                .Returns(_ => AsyncEnumerable.Empty<Transaction>());
        }

        IBillsService bills = Substitute.For<IBillsService>();
        bills.GetBillsAsync(Arg.Any<CancellationToken>()).Returns(_ => AsyncEnumerable.Empty<Bill>());
        IBudgetsService budgets = Substitute.For<IBudgetsService>();
        budgets.GetBudgetsAsync(Arg.Any<CancellationToken>()).Returns(_ => AsyncEnumerable.Empty<Budget>());
        IDebtsService debts = Substitute.For<IDebtsService>();
        debts.GetDebtsAsync(Arg.Any<CancellationToken>()).Returns(_ => AsyncEnumerable.Empty<Debt>());
        ISavingsGoalsService goals = Substitute.For<ISavingsGoalsService>();
        goals.GetSavingsGoalsAsync(Arg.Any<CancellationToken>()).Returns(_ => AsyncEnumerable.Empty<SavingsGoal>());
        IRulesService rules = Substitute.For<IRulesService>();
        rules.GetRulesAsync(Arg.Any<CancellationToken>()).Returns(_ => AsyncEnumerable.Empty<Rule>());
        IClassificationService tags = Substitute.For<IClassificationService>();
        tags.GetAllCustomClassificationsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<Contracts.Classifications.Classification>());
        ICalendarService calendar = Substitute.For<ICalendarService>();
        calendar.GetEventsAsync(Arg.Any<CancellationToken>()).Returns(_ => AsyncEnumerable.Empty<CalendarEvent>());
        IHouseholdMemberService household = Substitute.For<IHouseholdMemberService>();
        household.GetHouseholdMembersAsync(Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<HouseholdMember>());
        IPayPeriodsService pay = Substitute.For<IPayPeriodsService>();
        pay.GetYourPayAsync(Arg.Any<CancellationToken>()).Returns((YourPay)null!);
        INetWorthService netWorth = Substitute.For<INetWorthService>();
        netWorth.GetNetWorthAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>()).Returns(new NetWorthHistory());
        netWorth.GetBalanceHistoriesAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<AccountBalanceHistory>());

        return new BackupWriter(accounts, transactions, bills, budgets, debts, goals, rules, tags, calendar,
            household, pay, netWorth, new FixedTimeProvider(Now), sections);
    }

    private async Task<ZipArchive> WriteAsync(string? password = null)
    {
        MemoryStream backup = new();
        await BuildWriter(_transactionsService, _accountService).WriteAsync(backup, password, CancellationToken);
        backup.Position = 0;
        return new ZipArchive(backup, ZipArchiveMode.Read);
    }

    private static byte[] Bytes(ZipArchive zip, string name, string password)
    {
        using Stream entry = zip.GetEntry(name)!.Open(password);
        using MemoryStream copy = new();
        entry.CopyTo(copy);
        return copy.ToArray();
    }

    private static byte[] Bytes(ZipArchive zip, string name)
    {
        using Stream entry = zip.GetEntry(name)!.Open();
        using MemoryStream copy = new();
        entry.CopyTo(copy);
        return copy.ToArray();
    }

    private static string Text(ZipArchive zip, string name) =>
        System.Text.Encoding.UTF8.GetString(Bytes(zip, name));

    private static T Read<T>(ZipArchive zip, string name) =>
        JsonSerializer.Deserialize<T>(Bytes(zip, name), JsonSerializerOptions.Web)!;
}
