using FinanceTracker.Emails;
using FinanceTracker.Infrastructure.Account.Email;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Notifications.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications;

/// <summary>
/// The real email templates, rendered as the app renders them, for tests that look at what's sent.
/// </summary>
internal static class TestEmails
{
    public static NotificationEmails Create(Uri? publicUrl = null) => new(Renderer(), new PublicAddress(publicUrl));

    public static AccountEmails Account(Uri? publicUrl = null) => new(Renderer(), new PublicAddress(publicUrl));

    private static EmailRenderer Renderer() =>
        new(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLoggerFactory.Instance);
}
