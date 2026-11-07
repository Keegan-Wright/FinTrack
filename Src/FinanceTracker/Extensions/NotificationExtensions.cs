using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Notifications.Email;

namespace FinanceTracker.Extensions;

public static class NotificationExtensions
{
    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// Notifications pushed to user' browsers over the sync hub, and email through the mail server in the
        /// <c>SMTP_*</c> settings when there is one.
        /// </summary>
        public WebApplicationBuilder AddFinTrackNotifications()
        {
            builder.Services.AddSingleton(builder.Configuration.GetMailSettings());
            builder.Services.AddSingleton<IMailSender, SmtpMailSender>();
            builder.Services.AddSingleton<INotificationPublisher, HubNotificationPublisher>();
            return builder;
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
                From = from,
                PublicUrl = PublicUrlFrom(configuration[FinTrackEnvironment.PublicUrl])
            };
        }

        // Unset or empty, as Docker Compose passes an unset variable, means no links in emails
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
