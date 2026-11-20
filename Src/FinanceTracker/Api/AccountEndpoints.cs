using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Tokens;

namespace FinanceTracker.Api;

/// <summary>
/// Maps the account API.
/// </summary>
public static class AccountEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IAccountService"/> at <see cref="ApiRoutes.Accounts"/>.
        /// </summary>
        public RouteGroupBuilder MapAccountEndpoints()
        {
            RouteGroupBuilder accounts = api.MapGroup(ApiRoutes.Accounts)
                .ReadableWithToken();

            // GET ?transactions=5, each account with its 5 newest transactions
            accounts.MapGet(string.Empty, (int transactions, IAccountService service,
                    CancellationToken cancellationToken) =>
                service.GetAccountsAndMostRecentTransactionsAsync(transactions, cancellationToken));

            return api;
        }
    }
}
