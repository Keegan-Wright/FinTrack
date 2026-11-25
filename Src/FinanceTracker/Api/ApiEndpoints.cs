#if FINTRACK_AI
using FinanceTracker.Ai.Api;
using FinanceTracker.Ai.Mcp;
using FinanceTracker.Domain.Identity;
#endif
using FinanceTracker.Contracts.Api;
using FinanceTracker.Tokens;

namespace FinanceTracker.Api;

/// <summary>
/// Maps the whole JSON API.
/// </summary>
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
            // Signed in, and a personal access token only reads the endpoints opened to it, see TokenAccessHandler.
            // One policy saying both, as a second RequireAuthorization with its own policy drops the default's
            // signed in rule
            RouteGroupBuilder api = endpoints.MapGroup(string.Empty)
                .RequireAuthorization(static policy =>
                    policy.RequireAuthenticatedUser().AddRequirements(new TokenAccessRequirement()))
                .DisableCookieRedirect()
                .AddEndpointFilter<ApiRequestHeaderFilter>()
                .AddEndpointFilter<ExpectedFailureFilter>();

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
                .MapPushEndpoints()
                .MapProfileEndpoints()
                .MapPasskeyEndpoints()
                .MapReportEndpoints()
                .MapRuleEndpoints()
                .MapSavingsGoalEndpoints()
                .MapTokenEndpoints()
                .MapSharingEndpoints()
                .MapNetWorthEndpoints()
                .MapTransactionEndpoints()
                .MapTransferEndpoints()
                .MapUsersEndpoints();
#if FINTRACK_AI
            api.MapAiEndpoints();
#endif

            // An API address that isn't an endpoint answers a bare 404 for the app to read, rather than the front
            // end's not found page, whose catch-all route it otherwise matches
            api.Map("api/{**path}", static () => TypedResults.NotFound()).ExcludeFromDescription();

            endpoints.MapHub<SyncHub>(SyncHubContract.Path);
#if FINTRACK_AI

            // AI clients through MCP, signed in with a token made for them and only such a token. Never the browser's
            // cookie, which another site could otherwise send. See TokenAccessHandler
            endpoints.MapFinTrackMcp()
                .RequireAuthorization(static policy => policy
                    .AddAuthenticationSchemes(PersonalAccessTokenDefaults.Scheme)
                    .RequireAuthenticatedUser()
                    .AddRequirements(new TokenAccessRequirement()))
                .UsableWithToken(TokenScope.Ai);
#endif

            return endpoints;
        }
    }
}
