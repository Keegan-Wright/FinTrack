using FinanceTracker.Contracts.Exports;
using FinanceTracker.Contracts.PayPeriods;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Common;
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
    private readonly IBackupWriter _backupWriter;
    private readonly IStatementBuilder _statementBuilder;
    private readonly IStatementPeriods _statementPeriods;
    private readonly IReportDocumentBuilder _reportDocumentBuilder;
    private readonly TimeProvider _timeProvider;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ExportsService> _logger;

    /// <param name="transactionsService">Gives the transactions.</param>
    /// <param name="backupWriter">Writes the backup.</param>
    /// <param name="statementBuilder">Builds the statement.</param>
    /// <param name="statementPeriods">Works out the statement's months.</param>
    /// <param name="reportDocumentBuilder">Builds the report's PDF.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="currentUser">The signed in user, or who the work runs as.</param>
    /// <param name="logger">Its logger.</param>
    public ExportsService(ITransactionsService transactionsService, IBackupWriter backupWriter,
        IStatementBuilder statementBuilder, IStatementPeriods statementPeriods,
        IReportDocumentBuilder reportDocumentBuilder, TimeProvider timeProvider, ICurrentUser currentUser,
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

    /// <inheritdoc/>
    public async Task WriteTransactionsCsvAsync(FilterTransactions filter, Stream destination,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(destination);

        await TransactionCsv.WriteAsync(_transactionsService.GetAllTransactionsAsync(filter, cancellationToken),
            destination, _timeProvider.LocalTimeZone, cancellationToken);
        _logger.LogExported(_currentUser.UserId, "transactions as CSV");
    }

    /// <inheritdoc/>
    public async Task WriteBackupAsync(ExportBackup backup, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(backup);

        await _backupWriter.WriteAsync(destination, backup.Password, cancellationToken);
        if (backup.Password is null)
        {
            _logger.LogExported(_currentUser.UserId, "a backup");
            return;
        }

        _logger.LogExported(_currentUser.UserId, "a password protected backup");
    }

    /// <inheritdoc/>
    public Task<List<PayPeriod>> GetStatementPeriodsAsync(CancellationToken cancellationToken) =>
        _statementPeriods.GetAsync(cancellationToken);

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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
