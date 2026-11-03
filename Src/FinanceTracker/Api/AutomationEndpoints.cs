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
        /// <see cref="IAutomationService"/>: the background jobs at <see cref="ApiRoutes.AutomationJobs"/> and their runs at
        /// <see cref="ApiRoutes.AutomationRuns"/>, admins only as they run for all users, and the user's own syncs.
        /// </summary>
        public RouteGroupBuilder MapAutomationEndpoints()
        {
            RouteGroupBuilder jobs = api.MapGroup(ApiRoutes.AutomationJobs)
                .RequireAuthorization(policy => policy.RequireRole(FinTrackRoles.Admin));

            jobs.MapGet(string.Empty, (IAutomationService service, CancellationToken cancellationToken) =>
                service.GetJobsAsync(cancellationToken));

            jobs.MapGet("summaries", (IAutomationService service, CancellationToken cancellationToken) =>
                service.GetJobSummariesAsync(cancellationToken));

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

            // Queues a run on top of the schedule, unless one is already going
            jobs.MapPost("{id:guid}/run", async Task<Results<Accepted, ProblemHttpResult>> (Guid id,
                    IAutomationService service, CancellationToken cancellationToken) =>
                {
                    try
                    {
                        await service.RunNowAsync(id, cancellationToken);
                    }
                    catch (JobAlreadyRunningException ex)
                    {
                        return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
                    }

                    return TypedResults.Accepted((string?)null);
                })
                .NotFoundWhenMissing();

            RouteGroupBuilder runs = api.MapGroup(ApiRoutes.AutomationRuns)
                .RequireAuthorization(policy => policy.RequireRole(FinTrackRoles.Admin));

            // A job's runs newest first, ?function=SyncAllOpenBankingDetailsAsync&page=0&pageSize=25
            runs.MapGet(string.Empty, async Task<Results<Ok<AutomationRunPage>, ProblemHttpResult>> (string function,
                IAutomationService service, CancellationToken cancellationToken, int page = 0, int pageSize = 25) =>
            {
                if (page < 0 || pageSize < 1 || pageSize > IAutomationService.MaxRunsPageSize)
                {
                    return TypedResults.Problem(
                        $"The page can't be negative, and a page holds 1 to {IAutomationService.MaxRunsPageSize} runs.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                return TypedResults.Ok(await service.GetRunsAsync(function, page, pageSize, cancellationToken));
            });

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
