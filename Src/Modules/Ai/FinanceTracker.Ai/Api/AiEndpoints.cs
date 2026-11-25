using FinanceTracker.Ai.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FinanceTracker.Ai.Api;

/// <summary>
/// Maps the AI module's API onto the app's, so it shares the app's sign in, token and request rules.
/// </summary>
public static class AiEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IAiService"/>, the status and opt in for any signed in user.
        /// </summary>
        public RouteGroupBuilder MapAiEndpoints()
        {
            api.MapGet(AiRoutes.Status, ([FromServices] IAiService service, CancellationToken cancellationToken) =>
                service.GetStatusAsync(cancellationToken));

            api.MapPut(AiRoutes.OptIn, (ChangeAiOptIn change, [FromServices] IAiService service,
                    CancellationToken cancellationToken) =>
                service.ChangeOptInAsync(change, cancellationToken));

            return api;
        }
    }
}
