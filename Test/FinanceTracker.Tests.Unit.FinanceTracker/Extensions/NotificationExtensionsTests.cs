using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Extensions;
using FinanceTracker.Infrastructure.Notifications.Email;
using Microsoft.Extensions.Configuration;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Extensions;

public class NotificationExtensionsTests
{
    [Test]
    public async Task GetMailSettings_NoMailServer_SendsNoEmail()
    {
        // Act
        MailSettings settings = Configuration([]).GetMailSettings();

        // Assert
        await Assert.That(settings.IsConfigured).IsFalse();
    }

    [Test]
    public async Task GetMailSettings_MailServerWithoutFrom_SaysWhatsMissing()
    {
        // Arrange
        IConfiguration configuration = Configuration(new() { [FinTrackEnvironment.SmtpHost] = "mail.example.test" });

        // Act, Assert
        InvalidOperationException? exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() => Task.FromResult(configuration.GetMailSettings()));
        await Assert.That(exception!.Message).Contains(FinTrackEnvironment.SmtpFrom);
    }

    [Test]
    public async Task GetMailSettings_EverythingSet_ReadsIt()
    {
        // Arrange
        IConfiguration configuration = Configuration(new()
        {
            [FinTrackEnvironment.SmtpHost] = "mail.example.test",
            [FinTrackEnvironment.SmtpPort] = "465",
            [FinTrackEnvironment.SmtpSecurity] = "SslOnConnect",
            [FinTrackEnvironment.SmtpUsername] = "fintrack",
            [FinTrackEnvironment.SmtpPassword] = "placeholder",
            [FinTrackEnvironment.SmtpFrom] = "FinTrack <fintrack@example.test>",
            [FinTrackEnvironment.PublicUrl] = "https://fintrack.example.test"
        });

        // Act
        MailSettings settings = configuration.GetMailSettings();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings.IsConfigured).IsTrue();
            await Assert.That(settings.Port).IsEqualTo(465);
            await Assert.That(settings.Security).IsEqualTo(MailSecurity.SslOnConnect);
            await Assert.That(settings.Username).IsEqualTo("fintrack");
            await Assert.That(settings.From).IsEqualTo("FinTrack <fintrack@example.test>");
            await Assert.That(settings.PublicUrl).IsEqualTo(new Uri("https://fintrack.example.test/"));
        }
    }

    [Test]
    public async Task GetMailSettings_PublicUrlEmptyAsComposePassesIt_LinksToNothing()
    {
        // Act
        MailSettings settings = Configuration(new()
        {
            [FinTrackEnvironment.SmtpHost] = "mail.example.test",
            [FinTrackEnvironment.SmtpFrom] = "fintrack@example.test",
            [FinTrackEnvironment.PublicUrl] = ""
        }).GetMailSettings();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings.PublicUrl).IsNull();
            await Assert.That(settings.Port).IsEqualTo(587);
            await Assert.That(settings.Security).IsEqualTo(MailSecurity.Auto);
        }
    }

    [Test]
    public async Task GetMailSettings_PublicUrlWithASubPath_KeepsItForLinks()
    {
        // Act
        MailSettings settings = Configuration(new()
        {
            [FinTrackEnvironment.SmtpHost] = "mail.example.test",
            [FinTrackEnvironment.SmtpFrom] = "fintrack@example.test",
            [FinTrackEnvironment.PublicUrl] = "https://example.test/fintrack"
        }).GetMailSettings();

        // Assert
        await Assert.That(new Uri(settings.PublicUrl!, "Notifications").ToString())
            .IsEqualTo("https://example.test/fintrack/Notifications");
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
