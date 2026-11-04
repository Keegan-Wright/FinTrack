using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;

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

            return api;
        }
    }
}
