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
                .MapSavingsGoalEndpoints()
                .MapCalendarEndpoints()
                .MapClassificationEndpoints()
                .MapDashboardEndpoints()
                .MapHouseholdMemberEndpoints()
                .MapOpenBankingEndpoints()
                .MapReportEndpoints()
                .MapRuleEndpoints()
                .MapTransactionEndpoints()
                .MapTransferEndpoints();

            endpoints.MapHub<SyncHub>(SyncHubContract.Path);

            return endpoints;
        }
    }
}
