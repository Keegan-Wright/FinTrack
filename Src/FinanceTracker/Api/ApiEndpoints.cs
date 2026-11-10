using FinanceTracker.Contracts.Api;

namespace FinanceTracker.Api;

public static class ApiEndpoints
{
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// The JSON API over the <c>Contracts.Services</c> interfaces, for the front end once it runs in the browser,
        /// and the <see cref="SyncHub"/>. Every endpoint needs a signed in user and acts for them only, through the
        /// same services the server rendered pages use.
        /// </summary>
        public IEndpointRouteBuilder MapFinTrackApi()
        {
            RouteGroupBuilder api = endpoints.MapGroup(string.Empty)
                .RequireAuthorization()
                .DisableCookieRedirect()
                .AddEndpointFilter<ApiRequestHeaderFilter>();

            api.MapAccountEndpoints()
                .MapAutomationEndpoints()
                .MapBillEndpoints()
                .MapBudgetEndpoints()
                .MapCalendarEndpoints()
                .MapClassificationEndpoints()
                .MapClientErrorEndpoints()
                .MapDashboardEndpoints()
                .MapDebtEndpoints()
                .MapExportEndpoints()
                .MapHouseholdMemberEndpoints()
                .MapNotificationEndpoints()
                .MapOpenBankingEndpoints()
                .MapPayPeriodEndpoints()
                .MapProfileEndpoints()
                .MapReportEndpoints()
                .MapRuleEndpoints()
                .MapSavingsGoalEndpoints()
                .MapNetWorthEndpoints()
                .MapTransactionEndpoints()
                .MapTransferEndpoints()
                .MapUsersEndpoints();

            // An API address that isn't an endpoint answers a bare 404 for the app to read, rather than the front
            // end's not found page, whose catch-all route it otherwise matches
            api.Map("api/{**path}", static () => TypedResults.NotFound());

            endpoints.MapHub<SyncHub>(SyncHubContract.Path);

            return endpoints;
        }
    }
}
