using FinanceTracker.Ai.Contracts;
using FinanceTracker.Contracts.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
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
        /// <see cref="IAiService"/>. The status and opt in for any signed in user, the settings for admins only. An
        /// Ollama endpoint that isn't an absolute http or https address answers 400.
        /// </summary>
        public RouteGroupBuilder MapAiEndpoints()
        {
            api.MapGet(AiRoutes.Status, ([FromServices] IAiService service, CancellationToken cancellationToken) =>
                service.GetStatusAsync(cancellationToken));

            api.MapPut(AiRoutes.OptIn, (ChangeAiOptIn change, [FromServices] IAiService service,
                    CancellationToken cancellationToken) =>
                service.ChangeOptInAsync(change, cancellationToken));

            RouteGroupBuilder settings = api.MapGroup(AiRoutes.Settings)
                .RequireAuthorization(static policy => policy.RequireRole(FinTrackRoles.Admin));

            settings.MapGet(string.Empty, ([FromServices] IAiService service, CancellationToken cancellationToken) =>
                service.GetSettingsAsync(cancellationToken));

            settings.MapPut(string.Empty, SaveSettingsAsync);

            return api;
        }
    }

    /// <summary>
    /// Saves the settings, or says why the endpoint can't be used.
    /// </summary>
    /// <param name="settings">The new settings.</param>
    /// <param name="service">Saves them.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private static async Task<Results<Ok<AiSettings>, ValidationProblem>> SaveSettingsAsync(SaveAiSettings settings,
        [FromServices] IAiService service, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(settings.OllamaEndpoint) && !OllamaEndpoint.IsValid(settings.OllamaEndpoint))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(SaveAiSettings.OllamaEndpoint)] = [OllamaEndpoint.InvalidMessage]
            });
        }

        return TypedResults.Ok(await service.SaveSettingsAsync(settings, cancellationToken));
    }
}
