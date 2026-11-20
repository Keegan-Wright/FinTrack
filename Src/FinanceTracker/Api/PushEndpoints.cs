using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

/// <summary>
/// Maps the push notification API.
/// </summary>
public static class PushEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IPushService"/> at <see cref="ApiRoutes.Push"/>. Removing a device that doesn't exist or isn't
        /// the user's answers 404, a test without any devices 409.
        /// </summary>
        public RouteGroupBuilder MapPushEndpoints()
        {
            RouteGroupBuilder push = api.MapGroup(ApiRoutes.Push);

            push.MapGet("", async Task<Ok<PushSettings>> (IPushService service,
                    CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetSettingsAsync(cancellationToken)));

            push.MapPost("devices", async Task<Ok<PushDeviceAdded>> (AddPushDevice request, IPushService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.AddDeviceAsync(request, cancellationToken)))
                .Validated<AddPushDevice>();

            push.MapDelete("devices/{id:guid}", (Guid id, IPushService service,
                    CancellationToken cancellationToken) => service.RemoveDeviceAsync(id, cancellationToken))
                .NotFoundWhenNone();

            push.MapPut("lock-screen", async Task<Ok<PushSettings>> (ChangeLockScreen request,
                    IPushService service, CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.ChangeLockScreenAsync(request, cancellationToken)));

            push.MapPost("test", (IPushService service, CancellationToken cancellationToken) =>
                    service.SendTestAsync(cancellationToken))
                .ConflictWhenNone();

            return api;
        }
    }
}
