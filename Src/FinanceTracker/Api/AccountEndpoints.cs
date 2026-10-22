using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;

namespace FinanceTracker.Api;

public static class AccountEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IAccountService"/> at <see cref="ApiRoutes.Accounts"/>.
        /// </summary>
        public RouteGroupBuilder MapAccountEndpoints()
        {
            RouteGroupBuilder accounts = api.MapGroup(ApiRoutes.Accounts);

            // GET ?transactions=5, each account with its 5 newest transactions
            accounts.MapGet(string.Empty, (int transactions, IAccountService service,
                    CancellationToken cancellationToken) =>
                service.GetAccountsAndMostRecentTransactionsAsync(transactions, cancellationToken));

            return api;
        }
    }
}
