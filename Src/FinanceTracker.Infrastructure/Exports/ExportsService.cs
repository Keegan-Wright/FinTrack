using FinanceTracker.Contracts.Exports;
using FinanceTracker.Contracts.PayPeriods;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Exports.Reports;
using FinanceTracker.Infrastructure.Exports.Statements;
using QuestPDF.Fluent;

namespace FinanceTracker.Infrastructure.Exports;

/// <summary>
/// Exports through the same services the pages read, so an export is exactly what the user sees and only theirs.
/// Rows are written as they're read.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IExportsService>]
public sealed class ExportsService : IExportsService
{
    private readonly ITransactionsService _transactionsService;
    private readonly BackupWriter _backupWriter;
    private readonly StatementBuilder _statementBuilder;
    private readonly StatementPeriods _statementPeriods;
    private readonly ReportDocumentBuilder _reportDocumentBuilder;
    private readonly TimeProvider _timeProvider;

    public ExportsService(ITransactionsService transactionsService, BackupWriter backupWriter,
        StatementBuilder statementBuilder, StatementPeriods statementPeriods,
        ReportDocumentBuilder reportDocumentBuilder, TimeProvider timeProvider)
    {
        _transactionsService = transactionsService;
        _backupWriter = backupWriter;
        _statementBuilder = statementBuilder;
        _statementPeriods = statementPeriods;
        _reportDocumentBuilder = reportDocumentBuilder;
        _timeProvider = timeProvider;
    }

    public Task WriteTransactionsCsvAsync(FilterTransactions filter, Stream destination,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(destination);

        return TransactionCsv.WriteAsync(_transactionsService.GetAllTransactionsAsync(filter, cancellationToken),
            destination, _timeProvider.LocalTimeZone, cancellationToken);
    }

    public Task WriteBackupAsync(Stream destination, CancellationToken cancellationToken) =>
        _backupWriter.WriteAsync(destination, cancellationToken);

    public Task<List<PayPeriod>> GetStatementPeriodsAsync(CancellationToken cancellationToken) =>
        _statementPeriods.GetAsync(cancellationToken);

    public async Task WriteStatementAsync(int year, int month, Stream destination,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        Statement statement = await _statementBuilder.BuildAsync(year, month, cancellationToken);

        // QuestPDF writes synchronously, which an HTTP response refuses, so it's made whole then written
        byte[] pdf = new StatementDocument(statement, _timeProvider.LocalTimeZone).GeneratePdf();
        await destination.WriteAsync(pdf, cancellationToken);
    }

    public async Task WriteReportAsync(ExportReport report, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        ReportContent content = await _reportDocumentBuilder.BuildAsync(report, cancellationToken);

        // Made whole then written, as for the statement
        byte[] pdf = new ReportDocument(content).GeneratePdf();
        await destination.WriteAsync(pdf, cancellationToken);
    }
}
