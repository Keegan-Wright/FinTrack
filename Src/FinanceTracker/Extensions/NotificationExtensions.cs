using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Notifications.Email;
using FinanceTracker.Infrastructure.Notifications.Webhooks;

namespace FinanceTracker.Extensions;

public static class NotificationExtensions
{
    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// Notifications pushed to users' browsers over the sync hub, email through the mail server in the
        /// <c>SMTP_*</c> settings when there is one, and users' webhooks.
        /// </summary>
        public WebApplicationBuilder AddFinTrackNotifications()
        {
            builder.Services.AddSingleton(builder.Configuration.GetPublicAddress());
            builder.Services.AddSingleton(builder.Configuration.GetMailSettings());
            builder.Services.AddSingleton<IMailSender, SmtpMailSender>();
            builder.Services.AddSingleton<INotificationPublisher, HubNotificationPublisher>();

            builder.Services.AddSingleton(builder.Configuration.GetWebhookSettings());
            builder.Services.AddWebhookClient();
            return builder;
        }
    }

    extension(IServiceCollection services)
    {
        /// <summary>
        /// The client factory's <see cref="WebhookSender.ClientName"/> client: posts go through
        /// <see cref="WebhookHttp"/>'s handler, which only connects where <see cref="WebhookSettings"/> allows, and
        /// give up after <see cref="WebhookSender.Timeout"/>. Without the service defaults' retries, which would keep
        /// a test from the page waiting and post again to a receiver that may not expect a repeat.
        /// </summary>
        public IHttpClientBuilder AddWebhookClient()
        {
            IHttpClientBuilder client = services.AddHttpClient(WebhookSender.ClientName,
                    static http => http.Timeout = WebhookSender.Timeout)
                .ConfigurePrimaryHttpMessageHandler(static provider =>
                    WebhookHttp.CreateHandler(provider.GetRequiredService<WebhookSettings>()));

#pragma warning disable EXTEXP0001 // Experimental, but the supported way to take the defaults' handler off one client
            client.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

            return client;
        }
    }

    extension(IConfiguration configuration)
    {
        /// <summary>
        /// The mail server from the <c>SMTP_*</c> settings, <see cref="MailSettings.None"/> without
        /// <see cref="FinTrackEnvironment.SmtpHost"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">A mail server is set without who emails come from.</exception>
        public MailSettings GetMailSettings()
        {
            string? host = configuration[FinTrackEnvironment.SmtpHost];
            if (string.IsNullOrWhiteSpace(host))
            {
                return MailSettings.None;
            }

            string from = configuration[FinTrackEnvironment.SmtpFrom] is { Length: > 0 } configured
                ? configured
                : throw new InvalidOperationException(
                    $"{FinTrackEnvironment.SmtpFrom} is needed with {FinTrackEnvironment.SmtpHost}, who emails come from.");

            return new MailSettings
            {
                Host = host,
                Port = configuration.GetValue(FinTrackEnvironment.SmtpPort, 587),
                Security = configuration.GetValue(FinTrackEnvironment.SmtpSecurity, MailSecurity.Auto),
                Username = configuration[FinTrackEnvironment.SmtpUsername],
                Password = configuration[FinTrackEnvironment.SmtpPassword],
                From = from
            };
        }

        /// <summary>
        /// The app's address from <see cref="FinTrackEnvironment.PublicUrl"/>, for links in emails and webhook posts.
        /// </summary>
        /// <exception cref="InvalidOperationException">It's set but isn't an address.</exception>
        public PublicAddress GetPublicAddress() =>
            new(PublicUrlFrom(configuration[FinTrackEnvironment.PublicUrl]));

        /// <summary>
        /// Where webhooks may go, from <see cref="FinTrackEnvironment.WebhookAllowPrivate"/>, private addresses
        /// refused when it isn't set.
        /// </summary>
        public WebhookSettings GetWebhookSettings() => new()
        {
            AllowPrivateAddresses = configuration.GetValue(FinTrackEnvironment.WebhookAllowPrivate, false)
        };

        // Unset or empty, as Docker Compose passes an unset variable, means no links
        private static Uri? PublicUrlFrom(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            // Ending in / so links keep a sub path, e.g. https://example.com/fintrack/Notifications
            string withSlash = value.EndsWith('/') ? value : value + "/";
            if (!Uri.TryCreate(withSlash, UriKind.Absolute, out Uri? url))
            {
                throw new InvalidOperationException(
                    $"{FinTrackEnvironment.PublicUrl} should be the app's address, e.g. https://fintrack.example.com");
            }

            return url;
        }
    }
}
