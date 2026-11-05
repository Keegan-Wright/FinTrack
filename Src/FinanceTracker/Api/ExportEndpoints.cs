using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Exports;
using FinanceTracker.Contracts.PayPeriods;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

public static class ExportEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IExportsService"/> at <see cref="ApiRoutes.Exports"/>, each export streamed as a file download.
        /// </summary>
        public RouteGroupBuilder MapExportEndpoints()
        {
            RouteGroupBuilder exports = api.MapGroup(ApiRoutes.Exports);

            // POST so the filters go in the body, as the transactions search sends them
            exports.MapPost("transactions", (FilterTransactions request, IExportsService service,
                    TimeProvider timeProvider, CancellationToken cancellationToken) =>
                TypedResults.Stream(
                    stream => service.WriteTransactionsCsvAsync(request, stream, cancellationToken),
                    "text/csv; charset=utf-8", ExportFileNames.Transactions(timeProvider.GetLocalNow())));

            // A zip of everything the user has, see IExportsService.WriteBackupAsync
            exports.MapGet("backup", (IExportsService service, TimeProvider timeProvider,
                    CancellationToken cancellationToken) =>
                TypedResults.Stream(stream => service.WriteBackupAsync(stream, cancellationToken), "application/zip",
                    ExportFileNames.Backup(timeProvider.GetLocalNow())));

            // The periods there's a statement for, see IExportsService.GetStatementPeriodsAsync
            exports.MapGet("statement/periods", async Task<Ok<List<PayPeriod>>> (IExportsService service,
                    CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetStatementPeriodsAsync(cancellationToken)));

            // A PDF of the pay period named after the month, see IExportsService.WriteStatementAsync
            exports.MapGet("statement/{year:int:range(2000,2100)}/{month:int:range(1,12)}", (int year, int month,
                    IExportsService service, CancellationToken cancellationToken) =>
                TypedResults.Stream(stream => service.WriteStatementAsync(year, month, stream, cancellationToken),
                    "application/pdf", ExportFileNames.Statement(year, month)));

            // A PDF of the reports page's breakdown for its filters, POST as the filters go in the body
            exports.MapPost("report", (ExportReport request, IExportsService service, TimeProvider timeProvider,
                    CancellationToken cancellationToken) =>
                TypedResults.Stream(stream => service.WriteReportAsync(request, stream, cancellationToken),
                    "application/pdf", ExportFileNames.Report(request.Breakdown, timeProvider.GetLocalNow())));

            return api;
        }
    }
}
