using FinanceTracker.Contracts.Exports;
using FinanceTracker.Contracts.PayPeriods;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Exports.Reports;
using FinanceTracker.Infrastructure.Exports.Statements;
using Microsoft.Extensions.Logging;
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
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ExportsService> _logger;

    public ExportsService(ITransactionsService transactionsService, BackupWriter backupWriter,
        StatementBuilder statementBuilder, StatementPeriods statementPeriods,
        ReportDocumentBuilder reportDocumentBuilder, TimeProvider timeProvider, ICurrentUser currentUser,
        ILogger<ExportsService> logger)
    {
        _currentUser = currentUser;
        _logger = logger;
        _transactionsService = transactionsService;
        _backupWriter = backupWriter;
        _statementBuilder = statementBuilder;
        _statementPeriods = statementPeriods;
        _reportDocumentBuilder = reportDocumentBuilder;
        _timeProvider = timeProvider;
    }

    public async Task WriteTransactionsCsvAsync(FilterTransactions filter, Stream destination,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(destination);

        await TransactionCsv.WriteAsync(_transactionsService.GetAllTransactionsAsync(filter, cancellationToken),
            destination, _timeProvider.LocalTimeZone, cancellationToken);
        _logger.LogExported(_currentUser.UserId, "transactions as CSV");
    }

    public async Task WriteBackupAsync(Stream destination, CancellationToken cancellationToken)
    {
        await _backupWriter.WriteAsync(destination, cancellationToken);
        _logger.LogExported(_currentUser.UserId, "a backup");
    }

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
        _logger.LogExported(_currentUser.UserId, "a statement");
    }

    public async Task WriteReportAsync(ExportReport report, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        ReportContent content = await _reportDocumentBuilder.BuildAsync(report, cancellationToken);

        // Made whole then written, as for the statement
        byte[] pdf = new ReportDocument(content).GeneratePdf();
        await destination.WriteAsync(pdf, cancellationToken);
        _logger.LogExported(_currentUser.UserId, "a report");
    }
}
