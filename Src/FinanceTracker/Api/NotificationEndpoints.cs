using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

public static class NotificationEndpoints
{
    /// <summary>
    /// How many notifications the API gives when not asked for a number.
    /// </summary>
    private const int DefaultTake = 50;

    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="INotificationsService"/> at <see cref="ApiRoutes.Notifications"/>. Marking one that doesn't
        /// exist or isn't the user's answers 404. a test email when there's no mail server or address, or a webhook
        /// test or new secret without a webhook, 409.
        /// </summary>
        public RouteGroupBuilder MapNotificationEndpoints()
        {
            RouteGroupBuilder notifications = api.MapGroup(ApiRoutes.Notifications);

            notifications.MapGet("", (INotificationsService service, CancellationToken cancellationToken,
                    int? take) =>
                service.GetNotificationsAsync(take ?? DefaultTake, cancellationToken));

            notifications.MapGet("unread", async Task<Ok<int>> (INotificationsService service,
                    CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetUnreadCountAsync(cancellationToken)));

            notifications.MapPost("{id:guid}/read", async Task<Results<NoContent, NotFound>> (Guid id,
                INotificationsService service, CancellationToken cancellationToken) =>
            {
                if (!await service.MarkReadAsync(id, cancellationToken))
                {
                    return TypedResults.NotFound();
                }

                return TypedResults.NoContent();
            });

            notifications.MapPost("read", async Task<NoContent> (INotificationsService service,
                CancellationToken cancellationToken) =>
            {
                await service.MarkAllReadAsync(cancellationToken);
                return TypedResults.NoContent();
            });

            notifications.MapGet("settings", async Task<Ok<NotificationSettings>> (INotificationsService service,
                    CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetSettingsAsync(cancellationToken)));

            notifications.MapPut("settings", async Task<Ok<NotificationSettings>> (
                    ChangeNotificationSettings request, INotificationsService service,
                    CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.ChangeSettingsAsync(request, cancellationToken)));

            notifications.MapPut("settings/alerts", async Task<Ok<NotificationSettings>> (
                    ChangeAlertSettings request, INotificationsService service,
                    CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.ChangeAlertsAsync(request, cancellationToken)))
                .Validated<ChangeAlertSettings>();

            notifications.MapPost("settings/test-email", async Task<Results<Ok<NotificationSettings>, Conflict>> (
                INotificationsService service, CancellationToken cancellationToken) =>
            {
                if (await service.SendTestEmailAsync(cancellationToken) is not { } settings)
                {
                    return TypedResults.Conflict();
                }

                return TypedResults.Ok(settings);
            });

            // Setting answers 200 with the refusal when this FinTrack won't post there, see WebhookChange
            notifications.MapPut("settings/webhook", async Task<Ok<WebhookChange>> (SetWebhook request,
                    INotificationsService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.SetWebhookAsync(request, cancellationToken)))
                .Validated<SetWebhook>();

            notifications.MapDelete("settings/webhook", async Task<Ok<NotificationSettings>> (
                    INotificationsService service, CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.RemoveWebhookAsync(cancellationToken)));

            notifications.MapPost("settings/webhook/secret", async Task<Results<Ok<NotificationSettings>, Conflict>> (
                INotificationsService service, CancellationToken cancellationToken) =>
            {
                if (await service.ReplaceWebhookSecretAsync(cancellationToken) is not { } settings)
                {
                    return TypedResults.Conflict();
                }

                return TypedResults.Ok(settings);
            });

            notifications.MapPost("settings/webhook/test", async Task<Results<Ok<NotificationSettings>, Conflict>> (
                INotificationsService service, CancellationToken cancellationToken) =>
            {
                if (await service.SendTestWebhookAsync(cancellationToken) is not { } settings)
                {
                    return TypedResults.Conflict();
                }

                return TypedResults.Ok(settings);
            });

            return api;
        }
    }
}
