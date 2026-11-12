using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceTracker.Contracts.Exports;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Infrastructure.Exports;

/// <summary>
/// Writes a backup of everything the user has as a zip, see <see cref="IExportsService.WriteBackupAsync"/>. Each file
/// is read through the service its page reads, so it holds what the user sees, decrypted, and only theirs. Files are
/// written one at a time, transactions as they're read, and the manifest last so it can count them.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<BackupWriter>]
public sealed class BackupWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IAccountService _accountService;
    private readonly ITransactionsService _transactionsService;
    private readonly IBillsService _billsService;
    private readonly IBudgetsService _budgetsService;
    private readonly IDebtsService _debtsService;
    private readonly ISavingsGoalsService _savingsGoalsService;
    private readonly IRulesService _rulesService;
    private readonly IClassificationService _classificationService;
    private readonly ICalendarService _calendarService;
    private readonly IHouseholdMemberService _householdMemberService;
    private readonly IPayPeriodsService _payPeriodsService;
    private readonly INetWorthService _netWorthService;
    private readonly TimeProvider _timeProvider;

    public BackupWriter(IAccountService accountService, ITransactionsService transactionsService,
        IBillsService billsService, IBudgetsService budgetsService, IDebtsService debtsService,
        ISavingsGoalsService savingsGoalsService, IRulesService rulesService,
        IClassificationService classificationService, ICalendarService calendarService,
        IHouseholdMemberService householdMemberService, IPayPeriodsService payPeriodsService,
        INetWorthService netWorthService, TimeProvider timeProvider)
    {
        _accountService = accountService;
        _transactionsService = transactionsService;
        _billsService = billsService;
        _budgetsService = budgetsService;
        _debtsService = debtsService;
        _savingsGoalsService = savingsGoalsService;
        _rulesService = rulesService;
        _classificationService = classificationService;
        _calendarService = calendarService;
        _householdMemberService = householdMemberService;
        _payPeriodsService = payPeriodsService;
        _netWorthService = netWorthService;
        _timeProvider = timeProvider;
    }

    public async Task WriteAsync(Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        // Built in memory then copied, as .NET 10 closes each zip entry with a synchronous write, which an HTTP
        // response refuses. A backup compresses to a few MB, and the browser holds the whole file to save it anyway.
        using MemoryStream backup = new();
        await WriteZipAsync(backup, cancellationToken);
        backup.Position = 0;
        await backup.CopyToAsync(destination, cancellationToken);
    }

    private async Task WriteZipAsync(Stream destination, CancellationToken cancellationToken)
    {
        DateTimeOffset exportedAt = _timeProvider.GetUtcNow();
        Dictionary<string, int> files = [];

        await using ZipArchive zip = await ZipArchive.CreateAsync(destination, ZipArchiveMode.Create,
            leaveOpen: true, entryNameEncoding: null, cancellationToken);

        // Accounts without their transactions, which have a file of their own
        files["accounts.json"] = await WriteListAsync(zip, "accounts.json",
            _accountService.GetAccountsAndMostRecentTransactionsAsync(0, cancellationToken), cancellationToken);
        files["transactions.json"] = await WriteListAsync(zip, "transactions.json",
            _transactionsService.GetAllTransactionsAsync(new FilterTransactions(), cancellationToken),
            cancellationToken);
        files["transactions.csv"] = await WriteTransactionsCsvAsync(zip, cancellationToken);
        // Their own, not those someone else shares with them
        files["bills.json"] = await WriteListAsync(zip, "bills.json",
            _billsService.GetBillsAsync(cancellationToken).Where(static x => x.SharedBy is null),
            cancellationToken);
        files["budgets.json"] = await WriteListAsync(zip, "budgets.json",
            _budgetsService.GetBudgetsAsync(cancellationToken).Where(static x => x.SharedBy is null),
            cancellationToken);
        // Their own, not those someone else shares with them
        files["debts.json"] = await WriteListAsync(zip, "debts.json",
            _debtsService.GetDebtsAsync(cancellationToken).Where(static x => x.SharedBy is null),
            cancellationToken);
        files["savings-goals.json"] = await WriteListAsync(zip, "savings-goals.json",
            _savingsGoalsService.GetSavingsGoalsAsync(cancellationToken).Where(static x => x.SharedBy is null),
            cancellationToken);
        // Their own, not those someone else shares with them
        files["rules.json"] = await WriteListAsync(zip, "rules.json",
            _rulesService.GetRulesAsync(cancellationToken).Where(static x => x.SharedBy is null), cancellationToken);
        // Their own, not those someone else shares with them
        files["tags.json"] = await WriteListAsync(zip, "tags.json",
            _classificationService.GetAllCustomClassificationsAsync(cancellationToken)
                .Where(static x => x.SharedBy is null), cancellationToken);
        files["calendar-events.json"] = await WriteListAsync(zip, "calendar-events.json",
            _calendarService.GetEventsAsync(cancellationToken).Where(static x => x.SharedBy is null),
            cancellationToken);
        files["household.json"] = await WriteListAsync(zip, "household.json",
            _householdMemberService.GetHouseholdMembersAsync(cancellationToken), cancellationToken);
        files["pay.json"] = await WriteOneAsync(zip, "pay.json",
            await _payPeriodsService.GetYourPayAsync(cancellationToken), cancellationToken);

        // From the first snapshot, history that can't be rebuilt once the bank no longer sends those transactions
        files["net-worth.json"] = await WriteOneAsync(zip, "net-worth.json",
            await _netWorthService.GetNetWorthAsync(from: null, cancellationToken), cancellationToken);
        files["balance-history.json"] = await WriteListAsync(zip, "balance-history.json",
            _netWorthService.GetBalanceHistoriesAsync(from: null, cancellationToken), cancellationToken);

        await WriteOneAsync(zip, "manifest.json", new BackupManifest
        {
            AppVersion = AppVersion(),
            ExportedAt = exportedAt,
            Files = files
        }, cancellationToken);
    }

    /// <returns>How many records it holds.</returns>
    private static async Task<int> WriteListAsync<T>(ZipArchive zip, string name, IAsyncEnumerable<T> records,
        CancellationToken cancellationToken)
    {
        int count = 0;
        await using Stream entry = await zip.CreateEntry(name, CompressionLevel.Optimal).OpenAsync(cancellationToken);
        await JsonSerializer.SerializeAsync(entry, Counted(records, () => count++), Json, cancellationToken);
        return count;
    }

    /// <returns>1, the one record it holds.</returns>
    private static async Task<int> WriteOneAsync<T>(ZipArchive zip, string name, T record,
        CancellationToken cancellationToken)
    {
        await using Stream entry = await zip.CreateEntry(name, CompressionLevel.Optimal).OpenAsync(cancellationToken);
        await JsonSerializer.SerializeAsync(entry, record, Json, cancellationToken);
        return 1;
    }

    /// <returns>How many transactions it holds.</returns>
    private async Task<int> WriteTransactionsCsvAsync(ZipArchive zip, CancellationToken cancellationToken)
    {
        int count = 0;
        await using Stream entry = await zip.CreateEntry("transactions.csv", CompressionLevel.Optimal)
            .OpenAsync(cancellationToken);
        IAsyncEnumerable<Transaction> transactions =
            _transactionsService.GetAllTransactionsAsync(new FilterTransactions(), cancellationToken);
        await TransactionCsv.WriteAsync(Counted(transactions, () => count++), entry, _timeProvider.LocalTimeZone,
            cancellationToken);
        return count;
    }

    private static async IAsyncEnumerable<T> Counted<T>(IAsyncEnumerable<T> records, Action counted)
    {
        await foreach (T record in records)
        {
            counted();
            yield return record;
        }
    }

    private static string AppVersion() =>
        typeof(BackupWriter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";
}
