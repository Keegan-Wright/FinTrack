using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

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
    private readonly TimeProvider _timeProvider;

    public ExportsService(ITransactionsService transactionsService, TimeProvider timeProvider)
    {
        _transactionsService = transactionsService;
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
}
