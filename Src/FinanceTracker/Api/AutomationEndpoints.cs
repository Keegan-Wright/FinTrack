using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Auth;
using FinanceTracker.Contracts.Automation;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

public static class AutomationEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IAutomationService"/>: the background jobs at <see cref="ApiRoutes.AutomationJobs"/>, CRONS run for all users/>.
        /// </summary>
        public RouteGroupBuilder MapAutomationEndpoints()
        {
            RouteGroupBuilder jobs = api.MapGroup(ApiRoutes.AutomationJobs)
                .RequireAuthorization(policy => policy.RequireRole(FinTrackRoles.Admin));

            jobs.MapGet(string.Empty, (IAutomationService service, CancellationToken cancellationToken) =>
                service.GetJobsAsync(cancellationToken));

            jobs.MapPut("{id:guid}", async Task<Results<NoContent, ProblemHttpResult>> (Guid id, UpdateCronJob request,
                    IAutomationService service, CancellationToken cancellationToken) =>
                {
                    if (id != request.Id)
                    {
                        return TypedResults.Problem("The route and body job ids differ.",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    await service.UpdateJobSettingsAsync(request, cancellationToken);
                    return TypedResults.NoContent();
                })
                .Validated<UpdateCronJob>()
                .NotFoundWhenMissing();

            RouteGroupBuilder sync = api.MapGroup(ApiRoutes.Sync);

            // No content when the user's accounts have never synced
            sync.MapGet("last", async Task<Results<Ok<DateTime>, NoContent>> (IAutomationService service,
                CancellationToken cancellationToken) =>
            {
                DateTime? lastSync = await service.GetLastSyncTimeAsync(cancellationToken);
                return lastSync is null ? TypedResults.NoContent() : TypedResults.Ok(lastSync.Value);
            });

            // Queues the sync, the sync hub says when it's done
            sync.MapPost(string.Empty, async Task<Accepted> (IAutomationService service,
                CancellationToken cancellationToken) =>
            {
                await service.RequestSyncAsync(cancellationToken);
                return TypedResults.Accepted((string?)null);
            });

            return api;
        }
    }
}
