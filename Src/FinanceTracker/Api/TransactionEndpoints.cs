using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Api;

public static class TransactionEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="ITransactionsService"/> at <see cref="ApiRoutes.Transactions"/>.
        /// </summary>
        public RouteGroupBuilder MapTransactionEndpoints()
        {
            RouteGroupBuilder transactions = api.MapGroup(ApiRoutes.Transactions);

            // POST, the filters are the same body the reports take
            transactions.MapPost("search", (FilterTransactions request, ITransactionsService service,
                    CancellationToken cancellationToken) =>
                service.GetAllTransactionsAsync(request, cancellationToken));

            RouteGroupBuilder filters = transactions.MapGroup("filters");

            filters.MapGet("accounts", (ITransactionsService service, CancellationToken cancellationToken) =>
                service.GetAccountsForTransactionFiltersAsync(cancellationToken));

            filters.MapGet("providers", (ITransactionsService service, CancellationToken cancellationToken) =>
                service.GetProvidersForTransactionFiltersAsync(cancellationToken));

            filters.MapGet("types", (ITransactionsService service, CancellationToken cancellationToken) =>
                service.GetTypesForTransactionFiltersAsync(cancellationToken));

            filters.MapGet("categories", (ITransactionsService service, CancellationToken cancellationToken) =>
                service.GetCategoriesForTransactionFiltersAsync(cancellationToken));

            filters.MapGet("tags", (ITransactionsService service, CancellationToken cancellationToken) =>
                service.GetTagsForTransactionFiltersAsync(cancellationToken));

            return api;
        }
    }
}
